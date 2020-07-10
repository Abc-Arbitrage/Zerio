using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Abc.Zerio.Interop;
using HdrHistogram;

namespace Abc.Zerio.Channel
{
    public unsafe class RegisteredMemoryChannel
    {
        private readonly RegisteredMemoryChannelBuffer _buffer;
        private readonly RegisteredMemoryChannelReader _reader;
        private readonly RegisteredMemoryChannelWriter _writer;
        private readonly LongHistogram _histogram;

        public event ChannelFrameReadDelegate FrameRead;
        
        public RegisteredMemoryChannel(int partitionSize, int maxBatchSize)
        {
            _buffer = new RegisteredMemoryChannelBuffer(partitionSize);
            _reader = new RegisteredMemoryChannelReader(_buffer.ConsumerPartitionGroup, maxBatchSize);
            _writer = new RegisteredMemoryChannelWriter(_buffer.ProducerPartitionGroup);

            _reader.FrameRead += OnFrameRead;
            
            _histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
        }

        public void CleanupPartitions()
        {
            _reader.CleanupPartition();
        }

        internal RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        {
            return _buffer.CreateBufferSegmentDescriptor(frame);
        }
        
        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            // IMPORTANT:
            // Currently, the protocol leaks to the memory channel, as we have to acquire a frame that is large
            // enough to contain the length prefixed message bytes. But the acquired frame might be larger than
            // that, due to alignment constraints. It does mean that the frames that will be read on the other side
            // of the channel must be handled by a component that is aware of that. (See MessageFramer)

            var frameLength = sizeof(int) + messageBytes.Length;

            var frame = _writer.AcquireFrame(frameLength);
            if (frame.IsEmpty)
                throw new InvalidOperationException($"Unable to acquire frame for message, Length: {messageBytes.Length}");

            Unsafe.WriteUnaligned(frame.DataPosition - sizeof(long), Stopwatch.GetTimestamp());
            
            var isValid = false;
            try
            {
                Unsafe.Write(frame.DataPosition, messageBytes.Length);
                var span = new Span<byte>(frame.DataPosition + sizeof(int), (int)frame.DataLength - sizeof(int));
                messageBytes.CopyTo(span);

                isValid = true;
            }
            finally
            {
                frame.Publish(isValid);
            }
        }

        public bool TryPoll()
        {
            return _reader.TryReadFrameBatch();
        }

        private void OnFrameRead(FrameBlock frame, bool endOfBatch, bool cleanupNeeded)
        {
            // Temporary:
            var start = Unsafe.ReadUnaligned<long>(frame.DataPosition - sizeof(long));
            var rrt = Stopwatch.GetTimestamp() - start;
            var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);
            
            _histogram.RecordValue(micros);
            
            FrameRead?.Invoke(new ChannelFrame(frame.DataPosition, frame.FrameLength), endOfBatch, cleanupNeeded);
        }

        public void DisplayStats()
        {
            _histogram.OutputPercentileDistribution(Console.Out, 1);
        }

        public void Stop()
        {
            _buffer.Dispose();
        }

        public void ResetStats()
        {
            _histogram.Reset();
        }
    }
}
