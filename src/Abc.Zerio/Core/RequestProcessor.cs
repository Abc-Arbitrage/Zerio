using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class RequestProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly Dictionary<(int sessionId, RequestType), Action> _pendingFlushOperations = new Dictionary<(int sessionId, RequestType), Action>();
        private readonly ISessionManager _sessionManager;
        private readonly int _sendingBufferLength;
        private readonly int _maxSendBatchSize;

        private int _currentBatchSize;

        private RequestEntry* _batchingEntry;

        public RequestProcessor(ZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _maxSendBatchSize = configuration.MaxSendBatchSize;
            _sendingBufferLength = configuration.SendingBufferLength;
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(RequestProcessor);
        }

        public void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            switch (data.Type)
            {
                case RequestType.Send:
                    OnSendRequest(ref data, sequence, endOfBatch);
                    break;
                case RequestType.Receive:
                    OnReceiveRequest(ref data, endOfBatch);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnSendRequest(ref RequestEntry currentEntry, long sequence, bool endOfBatch)
        {
            bool currentEntryWasConsumed;

            if (_batchingEntry == null)
            {
                _batchingEntry = (RequestEntry*)Unsafe.AsPointer(ref currentEntry);
                currentEntryWasConsumed = true;
            }
            else
            {
                currentEntryWasConsumed = TryCopyCurrentEntryToBatchingEntry(ref currentEntry);
                if (currentEntryWasConsumed)
                    currentEntry.Type = RequestType.BatchedSend;
            }

            var shouldForwardToRioBatch = endOfBatch || !currentEntryWasConsumed;
            if (!shouldForwardToRioBatch)
                return;

            if (currentEntryWasConsumed)
            {
                AddToSendRioBatch(ref Unsafe.AsRef<RequestEntry>(_batchingEntry), sequence, endOfBatch);
            }
            else
            {
                AddToSendRioBatch(ref Unsafe.AsRef<RequestEntry>(_batchingEntry), sequence, false);
                AddToSendRioBatch(ref Unsafe.AsRef<RequestEntry>(_batchingEntry), sequence, endOfBatch);
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

        private void OnReceiveRequest(ref RequestEntry data, bool endOfBatch)
        {
            AddToReceiveRioBatch(ref data, endOfBatch);
        }

        private bool TryCopyCurrentEntryToBatchingEntry(ref RequestEntry currentEntry)
        {
            var currentEntryLength = currentEntry.RioBufferSegmentDescriptor.Length;
            if (currentEntryLength > _sendingBufferLength - _batchingEntry->RioBufferSegmentDescriptor.Length)
                return false;

            var endOfBatchingEntryData = _batchingEntry->GetBufferSegmentStart() + _batchingEntry->RioBufferSegmentDescriptor.Length;
            var startOfCurrentEntryData = currentEntry.GetBufferSegmentStart();

            Unsafe.CopyBlockUnaligned(endOfBatchingEntryData, startOfCurrentEntryData, (uint)currentEntryLength);
            _batchingEntry->RioBufferSegmentDescriptor.Length += currentEntryLength;

            return true;
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
