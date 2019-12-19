using System;
using System.Threading;
using Abc.Zerio.Configuration;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class Session
    {
        public int Id { get; }
        public string PeerId { get; set; }
        public RioRequestQueue RequestQueue => _requestQueue;

        private readonly IZerioConfiguration _configuration;
        private readonly RioObjects _rioObjects;
        
        private RioRequestQueue _requestQueue;
        private IntPtr _socket;

        public Session(int sessionId, IZerioConfiguration configuration, RioObjects rioObjects)
        {
            Id = sessionId;
            _configuration = configuration;
            _rioObjects = rioObjects;
        }

        public void Open(IntPtr socket)
        {
            Close();

            _socket = socket;

            var maxOutstandingReceives = (uint)_configuration.ReceivingBufferCount;
            var maxOutstandingSends = (uint)_configuration.SendingBufferCount;

            _requestQueue = RioRequestQueue.Create(Id, socket, _rioObjects.SendingCompletionQueue, maxOutstandingSends, _rioObjects.ReceivingCompletionQueue, maxOutstandingReceives);
        }

        private void Close()
        {
            if (Interlocked.Exchange(ref _requestQueue, null) == null)
                return;

            WinSock.closesocket(_socket);
            _socket = IntPtr.Zero;

            Closed(this);
        }

        public event Action<Session> Closed;

        public void InitiateReceiving(RequestProcessingEngine requestProcessingEngine)
        {
            for (var bufferSegmentId = 0; bufferSegmentId < _configuration.ReceivingBufferCount; bufferSegmentId++)
            {
                requestProcessingEngine.RequestReceive(Id, bufferSegmentId);
            }
        }
    }
}
