using System;
using System.Runtime.CompilerServices;

namespace Abc.Zerio.Channel
{
    public unsafe class RegisteredMemoryChannel
    {
        private readonly RegisteredMemoryChannelBuffer _buffer;
        private readonly RegisteredMemoryChannelReader _reader;
        private readonly RegisteredMemoryChannelWriter _writer;

        public event ChannelMessageReceivedDelegate MessageReceived;

        public RegisteredMemoryChannel(int partitionSize = 32 * 1024 * 2014)
        {
            _buffer = new RegisteredMemoryChannelBuffer(partitionSize);
            _reader = new RegisteredMemoryChannelReader(_buffer.ConsumerPartitionGroup);
            _writer = new RegisteredMemoryChannelWriter(_buffer.ProducerPartitionGroup);

            _reader.FrameRead += OnFrameRead;
        }

        public void CleanupPartitions()
        {
            _reader.CleanupPartition();
        }

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            var frameLength = sizeof(int) + messageBytes.Length;

            var frame = _writer.AcquireFrame(frameLength);
            if (frame.IsEmpty)
                throw new InvalidOperationException($"Unable to acquire frame for message, Length: {messageBytes.Length}");

            var isValid = false;
            try
            {
                Unsafe.Write(frame.DataPosition, messageBytes.Length);
                var span = new Span<byte>(frame.DataPosition + sizeof(int), (int)frame.DataLength);
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
            if (frame.DataLength < sizeof(int))
                throw new InvalidOperationException($"Invalid frame length DataLength: {frame.DataLength} FrameLength: {frame.FrameLength}");

            var messageLength = Unsafe.ReadUnaligned<int>(frame.DataPosition);
            MessageReceived?.Invoke(new ReadOnlySpan<byte>(frame.DataPosition + sizeof(int), messageLength), endOfBatch, cleanupNeeded);
        }

        public void Stop()
        {
            _buffer.Dispose();
        }
    }
}
