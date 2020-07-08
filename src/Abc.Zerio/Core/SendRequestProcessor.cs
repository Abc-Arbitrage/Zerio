using System.Collections.Generic;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal class SendRequestProcessor : IValueEventHandler<SendRequestEntry>, ILifecycleAware
    {
        private readonly HashSet<ISession> _sessionsWithPendingSends;
        private readonly ISessionManager _sessionManager;
        private readonly int _maxSendBatchSize;
        private readonly bool _batchSendRequests;
        private readonly bool _conflateSendRequestsOnProcessing;
        private readonly int _maxConflatedSendRequestCount;

        // private int _currentBatchSize;

        public SendRequestProcessor(InternalZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _batchSendRequests = configuration.BatchSendRequests;
            _conflateSendRequestsOnProcessing = configuration.ConflateSendRequestsOnProcessing;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
            _maxConflatedSendRequestCount = configuration.MaxConflatedSendRequestCount;

            _sessionManager = sessionManager;

            _sessionsWithPendingSends = new HashSet<ISession>(sessionManager.Sessions);
            _sessionsWithPendingSends.Clear(); // clear once preallocated 
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(SendRequestProcessor);
        }

        public void OnEvent(ref SendRequestEntry entry, long sequence, bool endOfBatch)
        {
            // if (!_sessionManager.TryGetSession(entry.SessionId, out var session))
            // {
            //     entry.EntryType = SendRequestEntryType.ExpiredSend;
            //     return;
            // }
            //
            // if (_conflateSendRequestsOnProcessing)
            //     ConflateAndEnqueueSendRequest(session, ref entry, sequence, endOfBatch);
            // else
            //     EnqueueSendRequest(session, ref entry, sequence, endOfBatch);
        }

        public void ConflateAndEnqueueSendRequest(ISession session, ref SendRequestEntry currentEntry, long sequence, bool endOfBatch)
        {
            //     bool currentEntryWasConsumed;
            //
            //     var sendingBatch = session.SendingBatch;
            //
            //     if (sendingBatch.IsEmpty)
            //     {
            //         sendingBatch.Initialize(ref currentEntry, sequence);
            //         currentEntryWasConsumed = true;
            //     }
            //     else
            //     {
            //         currentEntryWasConsumed = sendingBatch.TryAppend(ref currentEntry);
            //         if (currentEntryWasConsumed)
            //             currentEntry.EntryType = SendRequestEntryType.ConflatedSend;
            //     }
            //
            //     var shouldEnqueueToRioBatch = endOfBatch || !currentEntryWasConsumed || sendingBatch.Size > _maxConflatedSendRequestCount;
            //     if (!shouldEnqueueToRioBatch)
            //         return;
            //
            //     ref var batchingEntry = ref Unsafe.AsRef<SendRequestEntry>(sendingBatch.BatchingEntry);
            //     
            //     if (currentEntryWasConsumed)
            //     {
            //         EnqueueSendRequest(session, ref batchingEntry, sendingBatch.BatchingEntrySequence, true);
            //         sendingBatch.Reset();
            //     }
            //     else if (endOfBatch)
            //     {
            //         EnqueueSendRequest(session, ref batchingEntry, sendingBatch.BatchingEntrySequence, false);
            //         EnqueueSendRequest(session, ref currentEntry, sequence, true);
            //         sendingBatch.Reset();
            //     }
            //     else
            //     {
            //         EnqueueSendRequest(session, ref batchingEntry, sendingBatch.BatchingEntrySequence, false);
            //         sendingBatch.Initialize(ref currentEntry, sequence);
            //     }
            // }
            //
            // private void EnqueueSendRequest(ISession session, ref SendRequestEntry data, long sequence, bool endOfBatch)
            // {
            //     if (!_batchSendRequests)
            //     {
            //         session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), true);
            //         return;
            //     }
            //     
            //     var shouldFlush = endOfBatch || _maxSendBatchSize == _currentBatchSize;
            //     session.RequestQueue.Send(sequence, data.GetRioBufferDescriptor(), shouldFlush);
            //
            //     if (shouldFlush)
            //     {
            //         _sessionsWithPendingSends.Remove(session);
            //         FlushRequestQueues();
            //     }
            //     else
            //     {
            //         _sessionsWithPendingSends.Add(session);
            //         _currentBatchSize++;
            //     }
            // }
            //
            // private void FlushRequestQueues()
            // {
            //     foreach (var sessionWithPendingSends in _sessionsWithPendingSends)
            //     {
            //         sessionWithPendingSends.RequestQueue?.FlushSends();
            //     }
            //
            //     _currentBatchSize = 0;
            //     _sessionsWithPendingSends.Clear();
            // }
            //
        }

        public void OnShutdown()
        {
        }
    }
}
