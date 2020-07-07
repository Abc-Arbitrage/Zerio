using System;
using System.Threading;

namespace Abc.Zerio.Channel
{
    public unsafe class MemoryChannel
    {
        private ChannelMemoryBuffer _channelMemoryBuffer;
        private ChannelMemoryCleaner _cleaner;
        private ChannelMemoryReader _reader;
        private ChannelMemoryWriter _writer;
        
        private int _isRunning;
        private Thread _consumerThread;

        public void Start()
        {
            if (_channelMemoryBuffer != null)
                throw new InvalidOperationException("The transport is already started");

            _channelMemoryBuffer = new ChannelMemoryBuffer();

            _reader = new ChannelMemoryReader(_channelMemoryBuffer.ConsumerPartitionGroup);
            _writer = new ChannelMemoryWriter(_channelMemoryBuffer.ProducerPartitionGroup);
            
            if (Interlocked.Exchange(ref _isRunning, 1) != 0)
                return;

            _cleaner = new ChannelMemoryCleaner(_reader.Partitions);

            _consumerThread = new Thread(InboundReadThread) { IsBackground = true };
            _consumerThread.Start();
        }

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            var frame = _writer.AcquireFrame(messageBytes.Length);
            if (frame.IsEmpty)
                throw new InvalidOperationException($"Unable to acquire frame for message, Length: {messageBytes.Length}");

            var isValid = false;
            try
            {
                var span = new Span<byte>(frame.DataPosition, (int)frame.DataLength);
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
                var frame = _reader.TryReadNextFrame();
                if (frame.IsEmpty)
                {
                    spinWait.SpinOnce();
                    continue;
                }

                spinWait.Reset();

                if (frame.DataLength < sizeof(long) + sizeof(uint))
                    throw new InvalidOperationException($"Invalid frame length DataLength: {frame.DataLength} FrameLength: {frame.FrameLength}");

                OnMessageBytesReceived(new ReadOnlySpan<byte>(frame.DataPosition, (int)frame.DataLength));
            }
        }

        private void OnMessageBytesReceived(ReadOnlySpan<byte> messageBytes)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _isRunning, 0) != 1)
                return;

            _cleaner.Dispose();
            _channelMemoryBuffer.Dispose();
            
            _consumerThread?.Join();
        }
    }
}
