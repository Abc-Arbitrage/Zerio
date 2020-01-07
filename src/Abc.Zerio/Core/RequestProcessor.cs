using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class RequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly Dictionary<(int sessionId, RequestType), Action> _pendingFlushOperations;
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;
        private int _currentBatchSize;

        public RequestProcessor(ZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
            _pendingFlushOperations = new Dictionary<(int sessionId, RequestType), Action>(configuration.SessionCount * 2);
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(RequestProcessor);
        }

        public void OnEvent(ref RequestEntry entry, long sequence, bool endOfBatch)
        {
            if (!_sessionManager.TryGetSession(entry.SessionId, out var session))
            {
                entry.Type = RequestType.ExpiredOperation;
                return;
            }

            switch (entry.Type)
            {
                case RequestType.Send:
                    OnSendRequest(ref entry, sequence, endOfBatch, session);
                    break;
                case RequestType.Receive:
                    OnReceiveRequest(ref entry, endOfBatch, session);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnSendRequest(ref RequestEntry currentEntry, long sequence, bool endOfBatch, Session session)
        {
            bool currentEntryWasConsumed;

            var sendingBatch = session.SendingBatch;

            if (sendingBatch.IsEmpty)
            {
                sendingBatch.Initialize(ref currentEntry, sequence);
                currentEntryWasConsumed = true;
            }
            else
            {
                currentEntryWasConsumed = sendingBatch.TryAppend(ref currentEntry);
                if (currentEntryWasConsumed)
                    currentEntry.Type = RequestType.BatchedSend;
            }

            var shouldEnqueueToRioBatch = endOfBatch || !currentEntryWasConsumed;
            if (!shouldEnqueueToRioBatch)
                return;

            if (currentEntryWasConsumed)
            {
                AddToSendRioBatch(ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, true);
            }
            else
            {
                AddToSendRioBatch(ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, false);

                if (endOfBatch)
                {
                    AddToSendRioBatch(ref currentEntry, sequence, true);
                    sendingBatch.Reset();
                }
                else
                {
                    sendingBatch.Initialize(ref currentEntry, sequence);
                }
            }
        }

        private void AddToSendRioBatch(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;

            Action pendingFlushOperation = null;

            var sessionIsActive = _sessionManager.TryGetSession(data.SessionId, out var session);
            if (sessionIsActive)
            {
                session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);
                pendingFlushOperation = session.RequestQueue.FlushSendsOperation;
            }

            TryFlushRioBatches(ref data, RequestType.Send, shouldFlush, pendingFlushOperation);
        }

        private void AddToReceiveRioBatch(ref RequestEntry data, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;

            Action pendingFlushOperation = null;

            var sessionIsActive = _sessionManager.TryGetSession(data.SessionId, out var session);
            if (sessionIsActive)
            {
                session.RequestQueue.Receive(session.ReadBuffer(data.BufferSegmentId), data.BufferSegmentId, shouldFlush);
                pendingFlushOperation = session.RequestQueue.FlushReceivesOperation;
            }

            TryFlushRioBatches(ref data, RequestType.Receive, shouldFlush, pendingFlushOperation);
        }

        private void TryFlushRioBatches(ref RequestEntry data, RequestType requestType, bool shouldFlush, Action pendingFlushOperation)
        {
            var flushOperationKey = (data.SessionId, requestType);

            if (shouldFlush)
            {
                FlushRequestQueues(flushOperationKey);
            }
            else
            {
                if (pendingFlushOperation != null)
                    _pendingFlushOperations[flushOperationKey] = pendingFlushOperation;

                _currentBatchSize++;
            }
        }

        private void OnReceiveRequest(ref RequestEntry data, bool endOfBatch, Session session)
        {
            AddToReceiveRioBatch(ref data, endOfBatch);
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
