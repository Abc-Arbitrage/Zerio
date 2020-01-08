using System;
using System.Text;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class Session : IDisposable
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly UnmanagedRioBuffer<RioBufferSegment> _receivingBuffer;
        private readonly MessageFramer _messageFramer;

        private RioRequestQueue _requestQueue;
        private IntPtr _socket;
        private bool _isWaitingForHandshake = true; 

        public int Id { get; private set; }
        public string PeerId { get; private set; }
        public RioRequestQueue RequestQueue => _requestQueue;
        
        public event ServerMessageReceivedDelegate MessageReceived;
        public event Action<string> HandshakeReceived;
        public event Action<Session> Closed;

        public readonly SessionSendingBatch SendingBatch;
        
        public Session(int sessionId, InternalZerioConfiguration configuration, CompletionQueues completionQueues)
        {
            Id = sessionId;
            _configuration = configuration;
            _completionQueues = completionQueues;
            _receivingBuffer = new UnmanagedRioBuffer<RioBufferSegment>(configuration.ReceivingBufferCount, _configuration.ReceivingBufferLength);

            _messageFramer = new MessageFramer(configuration.FramingBufferLength);
            _messageFramer.MessageFramed += OnMessageFramed; 
            
            SendingBatch = new SessionSendingBatch(configuration.SendingBufferLength);
        }

        private void OnMessageFramed(ReadOnlySpan<byte> message)
        {
            if (_isWaitingForHandshake)
            {
                PeerId = Encoding.ASCII.GetString(message);
                HandshakeReceived?.Invoke(Encoding.ASCII.GetString(message));
                _isWaitingForHandshake = false;
                return;
            }
            
            MessageReceived?.Invoke(PeerId, message);
        }

        public void Open(IntPtr socket)
        {
            Close();

            _socket = socket;

            var maxOutstandingReceives = (uint)_configuration.ReceivingBufferCount * 2;
            var maxOutstandingSends = (uint)_configuration.SendingBufferCount * 2;

            _requestQueue = RioRequestQueue.Create(Id, socket, _completionQueues.SendingQueue, maxOutstandingSends, _completionQueues.ReceivingQueue, maxOutstandingReceives);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _requestQueue, null) == null)
                return;

            WinSock.closesocket(_socket);
            _socket = IntPtr.Zero;

            Closed(this);
        }

        public unsafe RioBufferSegment* ReadBuffer(int bufferSegmentId)
        {
            return _receivingBuffer[bufferSegmentId];
        }

        public void InitiateReceiving(RequestProcessingEngine requestProcessingEngine)
        {
            for (var bufferSegmentId = 0; bufferSegmentId < _configuration.ReceivingBufferCount; bufferSegmentId++)
            {
                requestProcessingEngine.RequestReceive(Id, bufferSegmentId);
            }
        }

        public void Dispose()
        {
            Close();
            
            _receivingBuffer?.Dispose();
        }

        public void Reset()
        {
            Id = default;
            _messageFramer.Reset();
            _isWaitingForHandshake = true;
        }

        public unsafe void OnBytesReceived(int bufferSegmentId, int bytesTransferred)
        {
            var bufferSegment = _receivingBuffer[bufferSegmentId];
            
            if (bufferSegment->RioBufferSegmentDescriptor.Length < bytesTransferred)
                throw new InvalidOperationException("Received more bytes than expected");

            var receivedBytes = new Span<byte>(bufferSegment->GetBufferSegmentStart(), bytesTransferred);
            _messageFramer.SubmitBytes(receivedBytes);
        }
    }
}
