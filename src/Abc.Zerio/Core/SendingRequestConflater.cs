using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Core
{
    internal unsafe class SendingRequestConflater
    {
        private readonly int _sessionId;
        private readonly int _bufferSegmentLength;
        private SpinLock _lock = new SpinLock(false);
        private volatile SendRequestEntry* _currentRequestEntry;

        public SendingRequestConflater(int sessionId, int bufferSegmentLength)
        {
            _sessionId = sessionId;
            _bufferSegmentLength = bufferSegmentLength;
        }

        public void EnqueueOrMergeSendRequest(ReadOnlySpan<byte> message, SendRequestProcessingEngine engine)
        {
            var lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                var currentEntry = _currentRequestEntry;

                if (currentEntry != null)
                {
                    if (TryAddMessageToExistingRequest(currentEntry, message))
                        return;
                }

                _currentRequestEntry = null;
            }
            finally
            {
                if (lockTaken)
                    _lock.Exit();
            }

            EnqueueNewMergeRequest(message, engine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAddMessageToExistingRequest(SendRequestEntry* currentEntry, ReadOnlySpan<byte> message)
        {
            if (message.Length > _bufferSegmentLength - currentEntry->RioBufferSegmentDescriptor.Length)
                return false;

            var endOfBatchingEntryData = currentEntry->GetBufferSegmentStart() + currentEntry->RioBufferSegmentDescriptor.Length;
            Unsafe.Write(endOfBatchingEntryData, message.Length);
            message.CopyTo(new Span<byte>(sizeof(int) +endOfBatchingEntryData, message.Length));
            currentEntry->RioBufferSegmentDescriptor.Length += sizeof(int) + message.Length;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnqueueNewMergeRequest(ReadOnlySpan<byte> message, SendRequestProcessingEngine engine)
        {
            using (var entry = engine.AcquireSendRequestEntry())
            {
                entry.Value->SetWriteRequest(_sessionId, message);
                _currentRequestEntry = entry.Value;
            }
        }

        public void DetachFrom(SendRequestEntry* entry)
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
