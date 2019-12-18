using System.Collections.Generic;
using System.Data;
using Abc.Zerio.Configuration;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal class RequestProcessor : IValueEventHandler<RequestEntry>
    {
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;

        private int _currentBatchSize;
        private readonly Dictionary<int, RioRequestQueue> _flushableRequestQueues = new Dictionary<int, RioRequestQueue>();

        public RequestProcessor(IZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = _maxSendBatchSize = configuration.MaxSendBatchSize;
        }

        public void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            switch (data.Type)
            {
                case RequestType.Receive:
                    OnReceivingRequest(data);
                    break;
                case RequestType.Send:
                    OnSendingRequest(ref data, sequence, endOfBatch);
                    break;
            }
        }

        private unsafe void OnSendingRequest(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;

            var requestQueueExists = _sessionManager.TryGetRequestQueue(data.SessionId, out var requestQueue);
            if (requestQueueExists)
                requestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);

            if (!shouldFlush)
            {
                _currentBatchSize++;

                if(requestQueueExists)
                    _flushableRequestQueues[data.SessionId] = requestQueue;

                return;
            }

            foreach (var (sessionId, flushableRequestQueue) in _flushableRequestQueues)
            {
                if (sessionId == data.SessionId)
                    continue;

                flushableRequestQueue.Flush();
            }

            _currentBatchSize = 0;
            _flushableRequestQueues.Clear();
        }

        private unsafe void OnReceivingRequest(RequestEntry data)
        {
            if (!_sessionManager.TryGetSession(data.SessionId, out var session))
                return;

            var buffer = session.ReadBuffer(data.BufferSegmentId);
            session.RequestQueue.Receive(buffer, data.BufferSegmentId);
        }
    }
}
