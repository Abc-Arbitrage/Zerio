using Disruptor;

namespace Abc.Zerio.Core
{
    public unsafe readonly ref struct AcquiredSendRequestEntry
    {
        private readonly UnmanagedRingBuffer<SendRequestEntry> _ringBuffer;
        private readonly long _sequence;
        
        public readonly SendRequestEntry* Value;

        public AcquiredSendRequestEntry(UnmanagedRingBuffer<SendRequestEntry> ringBuffer, long sequence, SendRequestEntry* value)
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
