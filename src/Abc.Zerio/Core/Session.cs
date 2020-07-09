using System;
using System.Text;
using System.Threading;
using Abc.Zerio.Channel;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class Session : ISession
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

        public event ServerMessageReceivedDelegate MessageReceived;
        public event Action<string> HandshakeReceived;
        public event Action<ISession> Closed;
        
        public RioRequestQueue RequestQueue => _requestQueue;
        public RegisteredMemoryChannel SendingChannel { get; }
        
        public Session(int sessionId, InternalZerioConfiguration configuration, CompletionQueues completionQueues)
        {
            Id = sessionId;
            _configuration = configuration;
            _completionQueues = completionQueues;
            
            _receivingBuffer = new UnmanagedRioBuffer<RioBufferSegment>(configuration.ReceivingBufferCount, _configuration.ReceivingBufferLength);

            _messageFramer = new MessageFramer(configuration.FramingBufferLength);
            _messageFramer.MessageFramed += OnMessageFramed;

            SendingChannel = new RegisteredMemoryChannel(configuration.ChannelPartitionSize, configuration.MaxFrameBatchSize);
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

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            SendingChannel.Send(messageBytes);
        }
        
        public void Open(IntPtr socket)
        {
            Close();

            _socket = socket;
            _requestQueue = RioRequestQueue.Create(Id,  socket, 
                                                   _completionQueues.SendingQueue, (uint)_configuration.RequestQueueMaxOutstandingSends,
                                                   _completionQueues.ReceivingQueue, (uint)_configuration.RequestQueueMaxOutstandingReceives);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _requestQueue, null) == null)
                return;

            WinSock.closesocket(_socket);
            _socket = IntPtr.Zero;

            Closed?.Invoke(this);
        }

        public void InitiateReceiving()
        {
            for (var bufferSegmentId = 0; bufferSegmentId < _configuration.ReceivingBufferCount; bufferSegmentId++)
            {
                RequestReceive(bufferSegmentId);
            }
        }

        public unsafe void OnBytesReceived(int bufferSegmentId, int bytesTransferred)
        {
            var bufferSegment = _receivingBuffer[bufferSegmentId];

            if (bufferSegment->RioBufferSegmentDescriptor.Length < bytesTransferred)
                throw new InvalidOperationException("Received more bytes than expected");

            var receivedBytes = new Span<byte>(bufferSegment->GetBufferSegmentStart(), bytesTransferred);
            _messageFramer.SubmitBytes(receivedBytes);
        }

        public unsafe void RequestReceive(int bufferSegmentId)
        {
            var bufferSegment = _receivingBuffer[bufferSegmentId];
            _requestQueue.Receive(bufferSegment, bufferSegmentId, true);
        }

        public void Reset()
        {
            Id = default;
            _messageFramer.Reset();
            _isWaitingForHandshake = true;
        }

        public void Dispose()
        {
            Close();

            SendingChannel?.Stop();
            _receivingBuffer?.Dispose();
        }
    }
}
