using Disruptor;

namespace Abc.Zerio.Core
{
    public unsafe readonly ref struct AcquiredRequestEntry
    {
        private readonly UnmanagedRingBuffer<RequestEntry> _ringBuffer;
        private readonly long _sequence;
        
        public readonly RequestEntry* Value;

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
