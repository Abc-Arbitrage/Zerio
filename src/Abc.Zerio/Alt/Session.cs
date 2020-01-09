using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Interop;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Abc.Zerio.Alt
{
    internal delegate void SessionMessageReceivedDelegate(string peerId, ReadOnlySpan<byte> message);

    internal unsafe class Session : IDisposable
    {
        public int SessionId { get; }

        private readonly RioBufferPools _pools;
        private readonly Poller _poller;

        private readonly RegisteredBuffer _srb;
        private readonly RegisteredBuffer _rrb;

        private readonly IntPtr _scq;
        private readonly IntPtr _rcq;
        private const int _sendPollCount = 128;
        private const int _receivePollCount = 128;

        private readonly IntPtr _rq;
        private readonly RioSendReceive _sendReceive;

        private readonly SemaphoreSlim _sendSemaphore;
        private int _isPollingSend;

        private readonly MessageFramer _messageFramer;

        public readonly AutoResetEvent HandshakeSignal = new AutoResetEvent(false);
        public string PeerId { get; private set; }

        private bool _isWaitingForHandshake = true;
        private readonly SessionMessageReceivedDelegate _messageReceived;
        private readonly Action<Session> _closed;
        private readonly byte* _resultsPtr;
        private readonly RIO_RESULT* _sendResults;
        private readonly RIO_RESULT* _receiveResults;

        public Session(int sessionId, IntPtr socket, RioBufferPools pools, Poller poller, SessionMessageReceivedDelegate messageReceived, Action<Session> closed)
        {
            SessionId = sessionId;
            _pools = pools;
            _poller = poller; // TODO remove this session on dispose
            _srb = pools.AllocateSendBuffer();
            _rrb = pools.AllocateReceiveBuffer();

            _messageReceived = messageReceived;
            _closed = closed;

            // This is a straightforward implementation that adds send requests
            // directly to RQ and polls a single CQ using the Poller.

            _sendSemaphore = new SemaphoreSlim(pools.SendSegmentCount * 2, pools.SendSegmentCount * 2);

            const int safeCacheLine = 128;
            _resultsPtr = (byte*)Marshal.AllocHGlobal(Unsafe.SizeOf<RIO_RESULT>() * (_sendPollCount + _receivePollCount) + safeCacheLine * 3);
            Debug.Assert(Utils.IsAligned((long)_resultsPtr, 8));
            //  128 ... send_results ... 128 ... receive_results ... 128
            _sendResults = (RIO_RESULT*)(_resultsPtr + safeCacheLine);
            _receiveResults = (RIO_RESULT*)((byte*)_sendResults + +(uint)Unsafe.SizeOf<RIO_RESULT>() * _sendPollCount + safeCacheLine);

            _scq = WinSock.Extensions.CreateCompletionQueue((uint)pools.SendSegmentCount * 2);
            _rcq = WinSock.Extensions.CreateCompletionQueue((uint)pools.ReceiveSegmentCount);

            _rq = WinSock.Extensions.CreateRequestQueue(socket,
                                                        (uint)pools.ReceiveSegmentCount,
                                                        1,
                                                        (uint)pools.SendSegmentCount * 2,
                                                        1,
                                                        _rcq,
                                                        _scq,
                                                        SessionId);

            _sendReceive = new RioSendReceive(_rq);

            for (int i = 0; i < pools.ReceiveSegmentCount; i++)
            {
                var segment = _pools.RentReceiveSegment();
                _sendReceive.Receive(segment);
            }

            _messageFramer = new MessageFramer(64 * 1024);
            _messageFramer.MessageFramed += OnMessageFramed;

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

            Marshal.FreeHGlobal((IntPtr)_resultsPtr);
        }

        public void Send(ReadOnlySpan<byte> message)
        {
            var claim = Claim();
            message.CopyTo(claim.Span);
            claim.Commit(message.Length, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Claim Claim()
        {
            RioSegment segment;

            bool entered;
            var count = 0;
            // total wait is (1 + spinLimit) * spinLimit / 2
            // 25 -> 325
            // 50 -> 1275
            // 100 -> 5050
            const int spinLimit = 50;
            while (true)
            {
                entered = _sendSemaphore.Wait(0);
                if (entered)
                    break;

                if (1 == Interlocked.Increment(ref _isPollingSend)) // 0 -> 1
                {
                    RIO_RESULT result = default;
                    var resultCount = WinSock.Extensions.DequeueCompletion(_scq, &result, 1);
                    Volatile.Write(ref _isPollingSend, 0);

                    if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                        WinSock.ThrowLastWsaError();

                    if (resultCount == 1)
                    {
                        var id = new BufferSegmentId(result.RequestCorrelation);
                        segment = _pools[id];
                        return new Claim(segment, this);
                    }
                }

                count++;
                if (count > spinLimit)
                    break;
                Thread.SpinWait(count);
            }

            if (!entered)
            {
                _sendSemaphore.Wait(_pools.CancellationToken);
            }

            segment = _pools.RentSendSegment();
            return new Claim(segment, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit(RioSegment segment)
        {
            _sendReceive.Send(segment);
        }

        /// <summary>
        /// This method is called by <see cref="Poller"/> on its thread.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Poll()
        {
            int count = 0;

            count += PollReceive();

            // poll send only if there are no receives
            if (count == 0)
                count += PollSend();

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PollReceive()
        {
            var resultCount = WinSock.Extensions.DequeueCompletion(_rcq, _receiveResults, _receivePollCount);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            for (int i = 0; i < resultCount; i++)
            {
                var result = _receiveResults[i];
                var id = new BufferSegmentId(result.RequestCorrelation);
                var segment = _pools[id];
                var span = segment.Span.Slice(0, (int)result.BytesTransferred);
                OnSegmentReceived(span);
                _sendReceive.Receive(segment);
            }

            return (int)resultCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PollSend()
        {
            if (1 != Interlocked.Increment(ref _isPollingSend))
                return 0;

            var resultCount = WinSock.Extensions.DequeueCompletion(_scq, _sendResults, _sendPollCount);

            Volatile.Write(ref _isPollingSend, 0);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            for (int i = 0; i < resultCount; i++)
            {
                var result = _sendResults[i];
                _sendSemaphore.Release();
                _pools.ReturnSegment(result.ConnectionCorrelation);
            }

            return (int)resultCount;
        }

        private void OnSegmentReceived(Span<byte> bytes)
        {
            _messageFramer.SubmitBytes(bytes);
        }

        private class RioSendReceive
        {
            private readonly IntPtr _rq;

            public RioSendReceive(IntPtr rq)
            {
                _rq = rq;
            }

            public void Send(RioSegment segment)
            {
                // SpinLock is not better, already tried
                lock (this)
                {
                    if (!WinSock.Extensions.Send(_rq, &segment.RioBuf, 1, RIO_SEND_FLAGS.DONT_NOTIFY, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }
            }

            public void Receive(RioSegment segment)
            {
                lock (this)
                {
                    if (!WinSock.Extensions.Receive(_rq, &segment.RioBuf, 1, RIO_RECEIVE_FLAGS.DONT_NOTIFY, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }
            }
        }
    }
}
