using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class BatchingSendRequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly Dictionary<int, Action> _pendingFlushOperations;
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;
        private int _currentBatchSize;

        public BatchingSendRequestProcessor(InternalZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
            _pendingFlushOperations = new Dictionary<int, Action>(configuration.SessionCount);
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(BatchingSendRequestProcessor);
        }

        public void OnEvent(ref RequestEntry entry, long sequence, bool endOfBatch)
        {
            if (!_sessionManager.TryGetSession(entry.SessionId, out var session))
            {
                entry.Type = RequestType.ExpiredOperation;
                return;
            }

            OnSendRequest(ref entry, sequence, endOfBatch, session);
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

            if (shouldFlush)
            {
                FlushRequestQueues(session.Id);
            }
            else
            {
                _pendingFlushOperations[session.Id] = session.RequestQueue.FlushSendsOperation;

                _currentBatchSize++;
            }
        }

        private void FlushRequestQueues(int noLongerPendingFlushOperationSessionId)
        {
            foreach (var (sessionId, pendingFlushOperation) in _pendingFlushOperations)
            {
                if (sessionId == noLongerPendingFlushOperationSessionId)
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
