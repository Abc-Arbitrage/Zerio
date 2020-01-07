using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Interop;

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
        private MessageFramer _messageFramer;

        public string PeerId { get; private set; }
        
        private bool _isWaitingForHandshake = true;
        private readonly Action<string> _handshakeReceived;
        private readonly SessionMessageReceivedDelegate _messageReceived;
        private readonly Action<Session> _closed;

        public unsafe Session(int sessionId, IntPtr socket, RioBufferPools pools, Poller poller, Action<string> handshakeReceived, SessionMessageReceivedDelegate messageReceived, Action<Session> closed)
        {
            SessionId = sessionId;
            _socket = socket;
            _pools = pools;
            _poller = poller;
            _srb = pools.AllocateSendBuffer();
            _rrb = pools.AllocateReceiveBuffer();

            _handshakeReceived = handshakeReceived;
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

            // this line at the very end after session is ready
            _poller.AddSession(this);
        }

        private void OnMessageFramed(ReadOnlySpan<byte> message)
        {
            if (_isWaitingForHandshake)
            {
                PeerId = Encoding.ASCII.GetString(message);
                _handshakeReceived?.Invoke(Encoding.ASCII.GetString(message));
                _isWaitingForHandshake = false;
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
            const int maxCompletionResults = 32;
            var results = stackalloc RIO_RESULT[maxCompletionResults];

            // dequeue some limited number from CQ
            var resultCount = WinSock.Extensions.DequeueCompletion(_cq, results, (uint)maxCompletionResults);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            for (int i = 0; i < resultCount; i++)
            {
                var result = results[i];
                var id = new BufferSegmentId(result.ConnectionCorrelation);
                if (id.PoolId == RioBufferPools.ReceivePoolId)
                {
                    var segment = _pools[id];
                    // this is payload from RIO buffer, not a message yet
                    var span = segment.Span.Slice(0, (int)result.BytesTransferred);

                    OnSegmentReceived(span);

                    // return receive segment back ro RQ
                    segment.RioBuf.Length = _pools.ReceiveSegmentSize;
                    _sr.Receive(segment);
                }
                else
                {
                    _pools.ReturnSegment(result.ConnectionCorrelation);
                }
            }

            return (int)resultCount;
        }

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
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);

                    if (!WinSock.Extensions.Send(_rq, &segment.RioBuf, 1, RIO_SEND_FLAGS.NONE, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }

            public void Receive(RioSegment segment)
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);

                    if (!WinSock.Extensions.Receive(_rq, &segment.RioBuf, 1, RIO_RECEIVE_FLAGS.NONE, segment.Id.Value))
                        WinSock.ThrowLastWsaError();
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }
        }
    }
}
