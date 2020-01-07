using System.Runtime.CompilerServices;

namespace Abc.Zerio.Core
{
    internal unsafe class SessionSendingBatch
    {
        private readonly int _bufferSegmentLength;

        public RequestEntry* BatchingEntry;
        public long BatchingEntrySequence;

        public bool IsEmpty => BatchingEntry == null;

        public SessionSendingBatch(int bufferSegmentLength)
        {
            _bufferSegmentLength = bufferSegmentLength;
        }

        public void Initialize(ref RequestEntry currentEntry, long sequence)
        {
            BatchingEntry = (RequestEntry*)Unsafe.AsPointer(ref currentEntry);
            BatchingEntrySequence = sequence;
        }

        public bool TryAppend(ref RequestEntry otherEntry)
        {
            var otherEntryLength = otherEntry.RioBufferSegmentDescriptor.Length;
            if (otherEntryLength > _bufferSegmentLength - BatchingEntry->RioBufferSegmentDescriptor.Length)
                return false;

            var endOfBatchingEntryData = BatchingEntry->GetBufferSegmentStart() + BatchingEntry->RioBufferSegmentDescriptor.Length;
            var startOfCurrentEntryData = otherEntry.GetBufferSegmentStart();

            Unsafe.CopyBlockUnaligned(endOfBatchingEntryData, startOfCurrentEntryData, (uint)otherEntryLength);
            BatchingEntry->RioBufferSegmentDescriptor.Length += otherEntryLength;

            return true;
        }

        public void Reset()
        {
            BatchingEntry = null;
            BatchingEntrySequence = default;
        }
    }
}
