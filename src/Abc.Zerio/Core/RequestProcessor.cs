using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using Abc.Zerio.Configuration;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal class RequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;
        private int _currentBatchSize;
        
        private readonly Dictionary<(int, RequestType), RioRequestQueue> _flushableRequestQueues = new Dictionary<(int, RequestType), RioRequestQueue>();

        public RequestProcessor(IZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = _maxSendBatchSize = configuration.MaxSendBatchSize;
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(RequestProcessor);
        }

        public unsafe void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;

            var requestType = data.Type;
            
            var sessionIsActive = _sessionManager.TryGetSession(data.SessionId, out var session);
            if (sessionIsActive)
            {
                switch (requestType)
                {
                    case RequestType.Receive:
                        session.RequestQueue.Receive(session.ReadBuffer(data.BufferSegmentId), data.BufferSegmentId, shouldFlush);
                        break;
                    case RequestType.Send:
                        session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);
                        break;
                    default:
                        throw new NetworkInformationException();
                }
            }

            var alreadyFlushedSessionId = (data.SessionId, requestType);

            if (shouldFlush)
            {
                FlushRequestQueues(alreadyFlushedSessionId);
            }
            else
            {
                if (sessionIsActive)
                    _flushableRequestQueues[alreadyFlushedSessionId] = session.RequestQueue;

                _currentBatchSize++;
            }
        }

        private void FlushRequestQueues((int SessionId, RequestType requestType) alreadyFlushedSessionId)
        {
            foreach (var (sessionId, requestQueue) in _flushableRequestQueues)
            {
                if (sessionId == alreadyFlushedSessionId)
                    continue;

                if (sessionId.Item2 == RequestType.Receive)
                    requestQueue.FlushReceives();
                else
                {
                    requestQueue.FlushSends();
                }
            }

            _currentBatchSize = 0;
            _flushableRequestQueues.Clear();
        }

        public void OnShutdown()
        {
        }
    }
}
