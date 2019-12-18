using System;
using System.Threading;
using Abc.Zerio.Configuration;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class ZerioSession : IDisposable
    {
        public int Id { get; }
        public string PeerId { get; set; }
        public RioRequestQueue RequestQueue => _requestQueue;

        private readonly IZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly RioBufferManager _receivingBufferManager;

        private RioRequestQueue _requestQueue;
        private IntPtr _socket;

        public ZerioSession(int sessionId, IZerioConfiguration configuration, CompletionQueues completionQueues)
        {
            Id = sessionId;
            _configuration = configuration;
            _completionQueues = completionQueues;
            _receivingBufferManager = RioBufferManager.Allocate(configuration.ReceivingBufferCount, _configuration.ReceivingBufferLength);
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

        public event Action<ZerioSession> Closed;

        public RioBuffer ReadBuffer(int bufferId)
        {
            return _receivingBufferManager.ReadBuffer(bufferId);
        }

        public void InitiateReceiving(RequestProcessingEngine requestProcessingEngine)
        {
            for (var i = 0; i < _configuration.ReceivingBufferCount; i++)
            {
                var buffer = _receivingBufferManager.AcquireBuffer(TimeSpan.FromSeconds(1));
                requestProcessingEngine.RequestReceive(Id, buffer.Id);
            }
        }

        public void Dispose()
        {
            _receivingBufferManager?.Dispose();
        }
    }
}
