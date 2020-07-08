using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Channel
{
    public unsafe class RegisteredMemoryChannel
    {
        private readonly int _partitionSize;
        private RegisteredMemoryChannelBuffer _buffer;
        private RegisteredMemoryChannelReader _reader;
        private RegisteredMemoryChannelWriter _writer;

        private int _isRunning;
        private Thread _consumerThread;

        public event ChannelMessageReceivedDelegate MessageReceived;

        public RegisteredMemoryChannel(int partitionSize = 32 * 1024 * 2014)
        {
            _partitionSize = partitionSize;
        }

        public void Start(bool manualPooling)
        {
            if (_buffer != null)
                throw new InvalidOperationException("The channel is already started");

            _buffer = new RegisteredMemoryChannelBuffer(_partitionSize);
            _reader = new RegisteredMemoryChannelReader(_buffer.ConsumerPartitionGroup);
            _writer = new RegisteredMemoryChannelWriter(_buffer.ProducerPartitionGroup);

            _reader.FrameRead += OnFrameRead;
            
            if (Interlocked.Exchange(ref _isRunning, 1) != 0)
                return;

            if (!manualPooling)
            {
                _consumerThread = new Thread(InboundReadThread) { IsBackground = true };
                _consumerThread.Start();
            }
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

        private void InboundReadThread()
        {
            var spinWait = new SpinWait();

            while (_isRunning == 1)
            {
                if (!_reader.TryReadFrameBatch())
                    spinWait.SpinOnce();

                spinWait.Reset();
            }
        }

        public bool TryPoll()
        {
            if (_isRunning == 0)
                throw new InvalidOperationException();

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
            if (Interlocked.Exchange(ref _isRunning, 0) != 1)
                return;

            _buffer.Dispose();

            _consumerThread?.Join();
        }
    }
}
