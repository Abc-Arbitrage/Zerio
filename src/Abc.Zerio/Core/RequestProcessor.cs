using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class RequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly ISessionManager _sessionManager;

        public RequestProcessor(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var sessionIsActive = _sessionManager.TryGetSession(data.SessionId, out var session);
            if (!sessionIsActive)
            {
                data.Type = RequestType.Undefined;
                return;
            }
            switch (data.Type)
            {
                case RequestType.Send:
                    session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), true);
                    break;
                case RequestType.Receive:
                    session.RequestQueue.Receive(session.ReadBuffer(data.BufferSegmentId), data.BufferSegmentId, true);
                    break;
            }
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(RequestProcessor);
        }

        public void OnShutdown()
        {
        }
    }
}
