using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Interop;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Abc.Zerio.Alt
{
    internal delegate void SessionMessageReceivedDelegate(string peerId, ReadOnlySpan<byte> message);

    internal unsafe class Session : SessionSCQLockRhsPad, IDisposable
    {
        private readonly BoundedLocalPool<RioSegment> _localSendPool;
        private readonly RegisteredBuffer _localSendBuffer;
        private readonly RegisteredBuffer _localReceiveBuffer;

        // private readonly RioBufferPool _globalPool;

        private readonly Poller _poller;

        private CancellationToken _ct;

        private readonly IntPtr _scq;
        private readonly IntPtr _rcq;
        private const int _sendPollCount = 64;
        private const int _receivePollCount = 64;

        private readonly IntPtr _rq; // TODO there is no Close method on RQ, review if need to do something else with it.
        private readonly RioSendReceive _sendReceive;

        private readonly SemaphoreSlimRhsPad _sendSemaphore;

        private readonly MessageFramer _messageFramer;

        public readonly AutoResetEvent HandshakeSignal = new AutoResetEvent(false);
        public string PeerId { get; private set; }

        private bool _isWaitingForHandshake = true;
        private readonly SessionMessageReceivedDelegate _messageReceived;
        private readonly Action<Session> _closed;
        private readonly byte* _resultsPtr;
        private readonly RIO_RESULT* _sendResults;
        private readonly RIO_RESULT* _receiveResults;

        public Session(IntPtr socket, RioBufferPool pool, Poller poller, SessionMessageReceivedDelegate messageReceived, Action<Session> closed)
        {
            var options = pool.Options;
            _ct = pool.CancellationToken;
            // _globalPool = pool;

            _localSendPool = new BoundedLocalPool<RioSegment>(options.SendSegmentCount);
            _localSendBuffer = new RegisteredBuffer(options.SendSegmentCount, options.SendSegmentSize);
            _localReceiveBuffer = new RegisteredBuffer(options.ReceiveSegmentCount, options.ReceiveSegmentSize);

            for (int i = 0; i < options.SendSegmentCount; i++)
            {
                _localSendPool.Return(_localSendBuffer[i]);
            }

            _messageReceived = messageReceived;
            _closed = closed;

            _sendSemaphore = new SemaphoreSlimRhsPad(_localSendPool.Count, _localSendPool.Count);

            _resultsPtr = (byte*)Marshal.AllocHGlobal(Unsafe.SizeOf<RIO_RESULT>() * (_sendPollCount + _receivePollCount) + Padding.SafeCacheLine * 3);
            //  128 ... send_results ... 128 ... receive_results ... 128
            _sendResults = (RIO_RESULT*)(_resultsPtr + Padding.SafeCacheLine);
            _receiveResults = (RIO_RESULT*)((byte*)_sendResults + +(uint)Unsafe.SizeOf<RIO_RESULT>() * _sendPollCount + Padding.SafeCacheLine);

            _scq = WinSock.Extensions.CreateCompletionQueue((uint)options.SendSegmentCount);
            _rcq = WinSock.Extensions.CreateCompletionQueue((uint)options.ReceiveSegmentCount);

            _rq = WinSock.Extensions.CreateRequestQueue(socket,
                                                        (uint)options.ReceiveSegmentCount,
                                                        1,
                                                        (uint)options.SendSegmentCount,
                                                        1,
                                                        _rcq,
                                                        _scq,
                                                        0);

            _sendReceive = new RioSendReceive(_rq);

            for (int i = 0; i < options.ReceiveSegmentCount; i++)
            {
                var segment = _localReceiveBuffer[i];
                _sendReceive.Receive(segment);
            }

            _messageFramer = new MessageFramer(64 * 1024);
            _messageFramer.MessageFramed += OnMessageFramed;

            _poller = poller;
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

        public void Send(ReadOnlySpan<byte> message)
        {
            var claim = Claim();
            message.CopyTo(claim.Span);
            claim.Commit(message.Length, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Claim Claim()
        {
            bool isPollerThread = Thread.CurrentThread.ManagedThreadId == _poller.ThreadId;
            var count = 0;
            const int spinLimit = 25;
            while (true)
            {
                bool entered = false;
                if (count >= spinLimit && !isPollerThread)
                {
                    _sendSemaphore.Wait(_ct);
                    entered = true;
                }
                else if (count == 0 || !isPollerThread)
                {
                    entered = _sendSemaphore.Wait(0);
                }

                RioSegment segment;
                if (entered)
                {
                    if (!_localSendPool.TryRent(out segment))
                    {
                        ThrowCannotGetSegmentAfterSemaphoreEnter();
                    }

                    return new Claim(segment, this);
                }

                if (isPollerThread
                    || 1 == Interlocked.Increment(ref _isPollingSend)) // 0 -> 1
                {
                    RIO_RESULT result = default;
                    var resultCount = WinSock.Extensions.DequeueCompletion(_scq, &result, 1);
                    Volatile.Write(ref _isPollingSend, 0);

                    if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                        WinSock.ThrowLastWsaError();

                    if (resultCount == 1)
                    {
                        var id = new BufferSegmentId(result.RequestCorrelation);
                        segment = _localSendBuffer[id.SegmentId]; //id.PoolId == 0 ? _localSendBuffer[id.SegmentId] : _globalPool[id];
                        return new Claim(segment, this);
                    }
                }

                count++;
                Thread.SpinWait(Math.Min(count, spinLimit));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCannotGetSegmentAfterSemaphoreEnter()
        {
            throw new InvalidOperationException("_localSendPool.TryRent(out segment)");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
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
                var segment = _localReceiveBuffer[id.SegmentId];
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
                var id = new BufferSegmentId(result.RequestCorrelation);
                if (id.PoolId == 0)
                {
                    var segment = _localSendBuffer[id.SegmentId];
                    _localSendPool.Return(segment);
                }
                else
                {
                    throw new InvalidOperationException();
                    //var segment = _globalPool[id];
                    //_globalPool.Return(segment);
                }

                // only after adding to the pool
                _sendSemaphore.Release();
            }

            return (int)resultCount;
        }

        private void OnSegmentReceived(Span<byte> bytes)
        {
            _messageFramer.SubmitBytes(bytes);
        }

        ~Session()
        {
            Dispose(false);
        }

        private void ReleaseUnmanagedResources()
        {
            Marshal.FreeHGlobal((IntPtr)_resultsPtr);
            WinSock.Extensions.CloseCompletionQueue(_rcq);
            WinSock.Extensions.CloseCompletionQueue(_scq);
        }

        private void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                _poller.RemoveSession(this);
                _closed.Invoke(this);
                // After setting this segments of these buffers won't be returned to the pools
                // and the buffers will be disposed as soon as no segments are used.

                // TODO drain outstanding

                _localReceiveBuffer.IsPooled = false;
                _localSendBuffer.IsPooled = false;
                _sendSemaphore?.Dispose();
                HandshakeSignal?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private class RioSendReceive
        {
             // TODO SpinLock with padding
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

        /// <summary>
        /// m_currentCount is at offset 32 with 12 bytes after in <see cref="SemaphoreSlim"/>. We cannot pad before, but at least
        /// </summary>
        private class SemaphoreSlimRhsPad : SemaphoreSlim
        {
#pragma warning disable 169
            private Padding _padding;
#pragma warning restore 169

            public SemaphoreSlimRhsPad(int initialCount, int maxCount) : base(initialCount, maxCount)
            {
            }
        }
    }

    // ReSharper disable InconsistentNaming

    [StructLayout(LayoutKind.Sequential, Size = SafeCacheLine - 8)]
    internal readonly struct Padding
    {
        public const int SafeCacheLine = 128;
    }

    internal class SessionLhsPad : CriticalFinalizerObject
    {
#pragma warning disable 169
        private Padding _padding;
#pragma warning restore 169
    }

    internal class SessionSCQLock : SessionLhsPad
    {
        protected int _isPollingSend;
#pragma warning disable 169
        private readonly int _padding;
#pragma warning restore 169
    }

    internal class SessionSCQLockRhsPad : SessionSCQLock
    {
#pragma warning disable 169
        private Padding _padding;
#pragma warning restore 169
    }

    // ReSharper restore InconsistentNaming
}
