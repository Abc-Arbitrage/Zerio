using System;
using System.Threading;

namespace Abc.Zerio.Core
{
    internal unsafe class SendingRequestConflater
    {
        private readonly int _sessionId;
        private readonly int _bufferSegmentLength;
        private SpinLock _lock = new SpinLock(false);
        private volatile RequestEntry* _currentRequestEntry;

        public SendingRequestConflater(int sessionId, int bufferSegmentLength)
        {
            _sessionId = sessionId;
            _bufferSegmentLength = bufferSegmentLength;
        }

        public void AddOrMerge(ReadOnlySpan<byte> message, SendRequestProcessingEngine engine)
        {
            var lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                var currentEntry = _currentRequestEntry;

                if (currentEntry != null)
                {
                    if (TryMerge(currentEntry, message))
                        return;
                }

                _currentRequestEntry = null;
            }
            finally
            {
                if (lockTaken)
                    _lock.Exit();
            }

            using (var entry = engine.AcquireRequestEntry())
            {
                entry.Value->SetWriteRequest(_sessionId, message);
                _currentRequestEntry = entry.Value;
            }
        }

        private bool TryMerge(RequestEntry* currentEntry, ReadOnlySpan<byte> message)
        {
            if (message.Length > _bufferSegmentLength - currentEntry->RioBufferSegmentDescriptor.Length)
                return false;

            var endOfBatchingEntryData = currentEntry->GetBufferSegmentStart() + currentEntry->RioBufferSegmentDescriptor.Length;
            message.CopyTo(new Span<byte>(endOfBatchingEntryData, message.Length));
            currentEntry->RioBufferSegmentDescriptor.Length += message.Length;
            return true;
        }

        public void DetachFrom(RequestEntry* entry)
        {
            var lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_currentRequestEntry == entry)
                    _currentRequestEntry = null;
            }
            finally
            {
                if (lockTaken)
                    _lock.Exit();
            }
        }
    }
}
