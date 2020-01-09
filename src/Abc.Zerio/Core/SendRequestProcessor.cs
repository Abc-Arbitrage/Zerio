using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class SendRequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly ISessionManager _sessionManager;

        public SendRequestProcessor(ISessionManager sessionManager)
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

            session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), true);
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(SendRequestProcessor);
        }

        public void OnShutdown()
        {
        }
    }
}
