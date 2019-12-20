using System;
using System.Threading;
using Abc.Zerio.Configuration;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class Session : IDisposable
    {
        private readonly IZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly UnmanagedRioBuffer<RioBufferSegment> _receivingBuffer;
        private readonly MessageFramer _messageFramer = new MessageFramer();

        private RioRequestQueue _requestQueue;
        private IntPtr _socket;
        
        public int Id { get; private set; }
        public string PeerId { get; set; }
        public RioRequestQueue RequestQueue => _requestQueue;
        
        public event ServerMessageReceivedDelegate MessageReceived;
        public event Action<Session> Closed;
        
        public Session(int sessionId, IZerioConfiguration configuration, CompletionQueues completionQueues)
        {
            Id = sessionId;
            _configuration = configuration;
            _completionQueues = completionQueues;
            _receivingBuffer = new UnmanagedRioBuffer<RioBufferSegment>(configuration.ReceivingBufferCount, _configuration.ReceivingBufferLength);
            _messageFramer.MessageFramed += message => MessageReceived?.Invoke(PeerId, message);
        }

        public void Open(IntPtr socket)
        {
            Close();

            _socket = socket;

            var maxOutstandingReceives = (uint)_configuration.ReceivingBufferCount;
            var maxOutstandingSends = (uint)_configuration.SendingBufferCount;

            _requestQueue = RioRequestQueue.Create(Id, socket, _completionQueues.SendingQueue, maxOutstandingSends, _completionQueues.ReceivingQueue, maxOutstandingReceives);
        }

        private void Close()
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
            _receivingBuffer?.Dispose();
        }

        public void Reset()
        {
            Id = default;
            _messageFramer.Reset();
        }

        public unsafe void OnBytesReceived(in int bufferSegmentId, in int bytesTransferred)
        {
            var bufferSegment = _receivingBuffer[bufferSegmentId];
            
            if (bufferSegment->RioBufferSegmentDescriptor.Length < bytesTransferred)
                throw new InvalidOperationException("Received more bytes than expected");

            _messageFramer.SubmitBytes(bufferSegment, bytesTransferred);
        }
    }
}
