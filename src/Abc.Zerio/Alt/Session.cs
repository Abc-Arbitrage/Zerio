﻿using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Alt.Collections;
using Abc.Zerio.Interop;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Abc.Zerio.Alt
{
    internal delegate void SessionMessageReceivedDelegate(string peerId, ReadOnlySpan<byte> message);

    internal class Session : IDisposable
    {
        public int SessionId { get; }

        private readonly IntPtr _socket;
        private readonly RioBufferPools _pools;
        private readonly Poller _poller;

        private RegisteredBuffer _srb;
        private RegisteredBuffer _rrb;

        private IntPtr _cq;
        private IntPtr _rq;
        private RioSendReceive _sr;

        private readonly SemaphoreSlim _sendSemaphore;

        private SingleProducerSingleConsumerQueue<RIO_RESULT> _receiveResultQueue = new SingleProducerSingleConsumerQueue<RIO_RESULT>();
        private SemaphoreSlim _processorSemaphore = new SemaphoreSlim(0, Int32.MaxValue);
        private Thread _processorThread;

        internal long RqSendLength => _pools.SendSegmentCount * 2;
        internal long RqReceiveLength => _pools.ReceiveSegmentCount;

        private MessageFramer _messageFramer;

        public readonly AutoResetEvent HandshakeSignal = new AutoResetEvent(false);
        public string PeerId { get; private set; }

        private bool _isWaitingForHandshake = true;
        private readonly SessionMessageReceivedDelegate _messageReceived;
        private readonly Action<Session> _closed;

        public unsafe Session(int sessionId, IntPtr socket, RioBufferPools pools, Poller poller, SessionMessageReceivedDelegate messageReceived, Action<Session> closed)
        {
            SessionId = sessionId;
            _socket = socket;
            _pools = pools;
            _poller = poller;
            _srb = pools.AllocateSendBuffer();
            _rrb = pools.AllocateReceiveBuffer();

            _messageReceived = messageReceived;
            _closed = closed;

            // This is a straightforward implementation that adds send requests
            // directly to RQ and polls a single CQ using the Poller.

            _sendSemaphore = new SemaphoreSlim(pools.SendSegmentCount * 2, pools.SendSegmentCount * 2);

            _cq = WinSock.Extensions.CreateCompletionQueue((uint)pools.ReceiveSegmentCount + (uint)pools.SendSegmentCount * 2);

            _rq = WinSock.Extensions.CreateRequestQueue(socket,
                                                        (uint)pools.ReceiveSegmentCount,
                                                        1,
                                                        (uint)pools.SendSegmentCount * 2,
                                                        1,
                                                        _cq,
                                                        _cq,
                                                        SessionId);

            _sr = new RioSendReceive(_rq);

            for (int i = 0; i < pools.ReceiveSegmentCount; i++)
            {
                var segment = _pools.RentReceiveSegment();
                _sr.Receive(segment);
            }

            _messageFramer = new MessageFramer(64 * 1024);
            _messageFramer.MessageFramed += OnMessageFramed;

            _processorThread = new Thread(InvocationLoop);
            _processorThread.Name = "invocation_thread";
            _processorThread.Priority = ThreadPriority.Highest;

            

            _processorThread.Start();

            // this line at the very end after session is ready
            _poller.AddSession(this);
        }

        private void OnMessageFramed(ReadOnlySpan<byte> message)
        {
            if (_isWaitingForHandshake)
            {
                PeerId = Encoding.ASCII.GetString(message);
                _isWaitingForHandshake = false;
                HandshakeSignal.Set();
                Send(message);
                return;
            }
            _messageReceived?.Invoke(PeerId, message);
        }

        public void Dispose()
        {
            // After setting this segments of these buffers won't be returned to the pools
            // and the buffers will be disposed as soon as no segments are used.
            _srb.IsPooled = false;
            _rrb.IsPooled = false;
        }

        public void Send(ReadOnlySpan<byte> message)
        {
            var claim = Claim();
            message.CopyTo(claim.Span);
            claim.Commit(message.Length, false);
        }

        public Claim Claim()
        {
            // This is thread-safe because pool is thread-safe.
            // But Claim/Commit pair of operation is not thread-safe.
            // Commit determines what is sent and in which order.
            RioSegment segment = _pools.RentSendSegment();
            return new Claim(segment, this);
        }

        public void Commit(RioSegment segment)
        {
            _sendSemaphore.Wait(_pools.CancellationToken);
            _sr.Send(segment);
        }

        /// <summary>
        /// This method is called by <see cref="Poller"/> on its thread.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Poll()
        {
            const int maxCompletionResults = 128;
            var results = stackalloc RIO_RESULT[maxCompletionResults];

            // dequeue some limited number from CQ
            var resultCount = WinSock.Extensions.DequeueCompletion(_cq, results, (uint)maxCompletionResults);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            for (int i = 0; i < resultCount; i++)
            {
                var result = results[i];
                var id = new BufferSegmentId(result.RequestCorrelation);
                if (id.PoolId == RioBufferPools.ReceivePoolId)
                {
                    _receiveResultQueue.Enqueue(result);
                    _processorSemaphore.Release();
                }
                //else
                //{
                //    _sendResultQueue.Enqueue(result);
                //}
            }

            var sendCount = 0;
            for (int i = 0; i < resultCount; i++)
            {
                var result = results[i];
                var id = new BufferSegmentId(result.RequestCorrelation);

                if (id.PoolId != RioBufferPools.ReceivePoolId)
                {
                    sendCount++;
                    // _receiveResultQueue.Enqueue(result);
                    // _processorSemaphore.Release();

                    _pools.ReturnSegment(result.ConnectionCorrelation);
                    //_sendSemaphore.Release();
                }
            }

            if (sendCount > 0)
            {
                _sendSemaphore.Release(sendCount);
            }

            return (int)resultCount;

            //int count = 0;

            //count += PollSend();
            //count += PollReceive();

            //return count;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public unsafe int PollReceive()
        //{
        //    const int maxCompletionResults = 1024;
        //    var results = stackalloc RIO_RESULT[maxCompletionResults];

        //    var resultCount = WinSock.Extensions.DequeueCompletion(_rcq, results, (uint)maxCompletionResults);

        //    if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
        //        WinSock.ThrowLastWsaError();

        //    for (int i = 0; i < resultCount; i++)
        //    {
        //        var result = results[i];
        //        _invokationQueue.Enqueue(result);
        //        _invocationSemaphore.Release();
        //    }
        //    return (int)resultCount;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public unsafe int PollSend()
        //{
        //    const int maxCompletionResults = 1024;
        //    var results = stackalloc RIO_RESULT[maxCompletionResults];

        //    // dequeue some limited number from CQ
        //    var resultCount = WinSock.Extensions.DequeueCompletion(_cq, results, (uint)maxCompletionResults);

        //    if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
        //        WinSock.ThrowLastWsaError();

        //    for (int i = 0; i < resultCount; i++)
        //    {
        //        var result = results[i];
        //        var id = new BufferSegmentId(result.RequestCorrelation);

        //        Debug.Assert(id.PoolId == RioBufferPools.SendPoolId);

        //        _sendSemaphore.Release();
        //        _pools.ReturnSegment(result.ConnectionCorrelation);
        //    }

        //    return (int)resultCount;
        //}

        private void OnSegmentReceived(Span<byte> bytes)
        {
            _messageFramer.SubmitBytes(bytes);
        }

        private unsafe class RioSendReceive
        {
            private readonly IntPtr _rq;
            private SpinLock _spinLock;

            public RioSendReceive(IntPtr rq)
            {
                _rq = rq;
                _spinLock = new SpinLock(false);
            }

            public void Send(RioSegment segment)
            {
                lock (this)
                {
                    if (!WinSock.Extensions.Send(_rq, &segment.RioBuf, 1, RIO_SEND_FLAGS.DONT_NOTIFY, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }

                //bool lockTaken = false;
                //try
                //{
                //    _spinLock.Enter(ref lockTaken);

                //    if (!WinSock.Extensions.Send(_rq, &segment.RioBuf, 1, RIO_SEND_FLAGS.NONE, segment.Id.Value))
                //        WinSock.ThrowLastWsaError();
                //}
                //finally
                //{
                //    if (lockTaken)
                //        _spinLock.Exit();
                //}
            }

            public void Receive(RioSegment segment)
            {
                lock (this)
                {
                    if (!WinSock.Extensions.Receive(_rq, &segment.RioBuf, 1, RIO_RECEIVE_FLAGS.DONT_NOTIFY, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }

                //bool lockTaken = false;
                //try
                //{
                //    _spinLock.Enter(ref lockTaken);

                //    // TODO Test DONT_NOTIFY
                //    if (!WinSock.Extensions.Receive(_rq, &segment.RioBuf, 1, RIO_RECEIVE_FLAGS.NONE, segment.Id.Value))
                //        WinSock.ThrowLastWsaError();
                //}
                //finally
                //{
                //    if (lockTaken)
                //        _spinLock.Exit();
                //}
            }
        }

        public void InvocationLoop()
        {
            //var nativeThread = CpuInfo.GetCurrentThread();
            //var affinity = CpuInfo.GetAffinity(0);
            //nativeThread.ProcessorAffinity = new IntPtr((long)affinity);

            // ReSharper disable once AssignmentInConditionalExpression
            while (true)
            {
                _processorSemaphore.Wait();

                while (_receiveResultQueue.TryDequeue(out var result))
                {
                    //var count = 0;
                    //var sw = new SpinWait();
                    //RIO_RESULT result = default;
                    //var breakX = false;
                    //while (!_receiveResultQueue.TryDequeue(out result))
                    //{
                    //    sw.SpinOnce();
                    //    if (sw.NextSpinWillYield)
                    //    {
                    //        count++;
                    //        if (count < 10)
                    //        {
                    //            sw.Reset();
                    //        }
                    //        else
                    //        {
                    //            breakX = true;
                    //            break;
                    //        }
                    //    }
                    //}

                    //if (breakX)
                    //    break;
                    var id = new BufferSegmentId(result.RequestCorrelation);
                    //if (id.PoolId != RioBufferPools.ReceivePoolId)
                    //{
                    //    _pools.ReturnSegment(result.ConnectionCorrelation);
                    //    _sendSemaphore.Release();
                    //}
                    //else
                    {
                        var segment = _pools[id];
                        var span = segment.Span.Slice(0, (int)result.BytesTransferred);
                        OnSegmentReceived(span);
                        // return receive segment back ro RQ
                        _sr.Receive(segment);
                    }

                    //var id = new BufferSegmentId(result.RequestCorrelation);
                    //Debug.Assert(id.PoolId == RioBufferPools.ReceivePoolId);
                    //var segment = _pools[id];

                    //// this is payload from RIO buffer, not a message yet
                    //var span = segment.Span.Slice(0, (int)result.BytesTransferred);
                    //OnSegmentReceived(span);

                    //// return receive segment back ro RQ
                    //_sr.Receive(segment);
                }
                
            }
        }
    }
}
