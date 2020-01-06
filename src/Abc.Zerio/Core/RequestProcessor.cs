using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal class RequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly Dictionary<(int sessionId, RequestType), Action> _pendingFlushOperations = new Dictionary<(int sessionId, RequestType), Action>();
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;

        private int _currentBatchSize;

        public RequestProcessor(ZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(RequestProcessor);
        }

        public unsafe void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;
            var requestType = data.Type;

            Action pendingFlushOperation = default;
            
            var sessionIsActive = _sessionManager.TryGetSession(data.SessionId, out var session);
            if (sessionIsActive)
            {
                switch (requestType)
                {
                    case RequestType.Receive:
                        session.RequestQueue.Receive(session.ReadBuffer(data.BufferSegmentId), data.BufferSegmentId, shouldFlush);
                        pendingFlushOperation = session.RequestQueue.FlushReceivesOperation;
                        break;
                    case RequestType.Send:
                        session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);
                        pendingFlushOperation = session.RequestQueue.FlushSendsOperation;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var flushOperationKey = (data.SessionId, requestType);

            if (shouldFlush)
            {
                FlushRequestQueues(flushOperationKey);
            }
            else
            {
                if (sessionIsActive)
                    _pendingFlushOperations[flushOperationKey] = pendingFlushOperation;

                _currentBatchSize++;
            }
        }

        private void FlushRequestQueues((int sessionId, RequestType requestType) noLongerPendingFlushOperationKey)
        {
            foreach (var (pendingFlushOperationKey, pendingFlushOperation) in _pendingFlushOperations)
            {
                if (pendingFlushOperationKey == noLongerPendingFlushOperationKey)
                    continue;

                pendingFlushOperation.Invoke();
            }

            _currentBatchSize = 0;
            _pendingFlushOperations.Clear();
        }

        public void OnShutdown()
        {
        }
    }
}
