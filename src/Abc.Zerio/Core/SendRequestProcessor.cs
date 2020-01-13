using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class SendRequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly Dictionary<int, Action> _pendingFlushOperations;
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;
        private readonly int _maxConflation;
        
        private int _currentBatchSize;

        private readonly bool _batchSendRequests;
        private readonly bool _conflateSendRequestsOnProcessing;
        private readonly bool _conflateSendRequestsOnEnqueuing;

        public SendRequestProcessor(InternalZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _batchSendRequests = configuration.BatchSendRequests;
            _conflateSendRequestsOnProcessing = configuration.ConflateSendRequestsOnProcessing;
            _conflateSendRequestsOnEnqueuing = configuration.ConflateSendRequestsOnEnqueuing;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
            _maxConflation = configuration.MaxConflationSendRequestCount;

            _sessionManager = sessionManager;
            _pendingFlushOperations = new Dictionary<int, Action>(configuration.SessionCount);
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(SendRequestProcessor);
        }

        public void OnEvent(ref RequestEntry entry, long sequence, bool endOfBatch)
        {   
            if (!_sessionManager.TryGetSession(entry.SessionId, out var session))
            {
                entry.Type = RequestType.ExpiredOperation;
                return;
            }

            if(_conflateSendRequestsOnEnqueuing)
                session.Conflater.DetachFrom((RequestEntry*)Unsafe.AsPointer(ref entry));
            
            if (_conflateSendRequestsOnProcessing)
                ConflateAndEnqueueSendRequest(session, ref entry, sequence, endOfBatch);
            else
                EnqueueSendRequest(session, ref entry, sequence, endOfBatch);
        }

        public void ConflateAndEnqueueSendRequest(Session session, ref RequestEntry entry, long sequence, bool endOfBatch)
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

            var shouldEnqueueToRioBatch = endOfBatch || !currentEntryWasConsumed || sendingBatch.Size > _maxConflation;
            if (!shouldEnqueueToRioBatch)
                return;

            if (currentEntryWasConsumed)
            {
                EnqueueSendRequest(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, true);
                sendingBatch.Reset();
            }
            else if (endOfBatch)
            {
                EnqueueSendRequest(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, false);
                EnqueueSendRequest(session, ref entry, sequence, true);
                sendingBatch.Reset();
            }
            else
            {
                EnqueueSendRequest(session, ref Unsafe.AsRef<RequestEntry>(sendingBatch.BatchingEntry), sendingBatch.BatchingEntrySequence, false);
                sendingBatch.Initialize(ref entry, sequence);
            }
        }

        private void EnqueueSendRequest(Session session, ref RequestEntry data, long sequence, bool endOfBatch)
        {
            if (!_batchSendRequests)
            {
                session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), true);
                return;
            }

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
