using Disruptor;

namespace Abc.Zerio.Core
{
    public unsafe ref struct AcquiredRequestEntry
    {
        public readonly RequestEntry* Value;

        private readonly ISequenced _ringBuffer;
        private readonly long _sequence;

        public AcquiredRequestEntry(UnmanagedRingBuffer<RequestEntry> ringBuffer, long sequence, RequestEntry* value)
        {
            _ringBuffer = ringBuffer;
            _sequence = sequence;
            Value = value;
        }

        public void Dispose()
        {
            _ringBuffer.Publish(_sequence);
        }
    }
}
