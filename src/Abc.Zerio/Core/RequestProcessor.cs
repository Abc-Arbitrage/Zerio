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

        public RequestProcessor(InternalZerioConfiguration configuration, ISessionManager sessionManager)
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
                case RequestType.Receive:
                    OnReceiveRequest(ref entry, endOfBatch, session);
                    break;
                case RequestType.Send:
                    OnSendRequest(ref entry, sequence, endOfBatch, session);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnReceiveRequest(ref RequestEntry entry, bool endOfBatch, Session session)
        {
            EnqueueToRioReceiveBatch(session, ref entry, endOfBatch);
        }

        public void OnSendRequest(ref RequestEntry entry, long sequence, bool endOfBatch, Session session)
        {
            bool currentEntryWasConsumed;

            var sendingBatch = session.SendingBatch;

            if (sendingBatch.IsEmpty)
            {
                sendingBatch.Initialize(ref entry, sequence);
                currentEntryWasConsumed = true;
            }
            else
            {
                currentEntryWasConsumed = sendingBatch.TryAppend(ref entry);
                if (currentEntryWasConsumed)
                    entry.Type = RequestType.AddedToSendBatch;
            }

            var shouldEnqueueToRioBatch = endOfBatch || !currentEntryWasConsumed;
            if (!shouldEnqueueToRioBatch)
                return;

            if (currentEntryWasConsumed)
            {
                EnqueueToRioSendBatch(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, true);
                sendingBatch.Reset();
            }
            else if (endOfBatch)
            {
                EnqueueToRioSendBatch(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, false);
                EnqueueToRioSendBatch(session, ref entry, sequence, true);
                sendingBatch.Reset();
            }
            else
            {
                EnqueueToRioSendBatch(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, false);
                sendingBatch.Initialize(ref entry, sequence);
            }
        }

        private void EnqueueToRioSendBatch(Session session, ref RequestEntry data, long sequence, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;
            session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);
            TryToFlushRioBatches(ref data, RequestType.Send, shouldFlush, session.RequestQueue.FlushSendsOperation);
        }

        private void EnqueueToRioReceiveBatch(Session session, ref RequestEntry data, bool endOfBatch)
        {
            var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;
            session.RequestQueue.Receive(session.ReadBuffer(data.BufferSegmentId), data.BufferSegmentId, shouldFlush);
            TryToFlushRioBatches(ref data, RequestType.Receive, shouldFlush, session.RequestQueue.FlushReceivesOperation);
        }

        private void TryToFlushRioBatches(ref RequestEntry data, RequestType requestType, bool shouldFlush, Action pendingFlushOperation)
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
