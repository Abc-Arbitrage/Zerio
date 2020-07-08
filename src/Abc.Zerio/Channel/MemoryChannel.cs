using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Abc.Zerio.Core;

namespace Abc.Zerio.Channel
{
    public unsafe class MemoryChannel
    {
        private ChannelMemoryBuffer _buffer;
        private ChannelMemoryCleaner _cleaner;
        private ChannelMemoryReader _reader;
        private ChannelMemoryWriter _writer;
        
        private int _isRunning;
        private Thread _consumerThread;

        public event ClientMessageReceivedDelegate MessageReceived;
        
        public void Start(bool manualPooling)
        {
            if (_buffer != null)
                throw new InvalidOperationException("The channel is already started");

            _buffer = new ChannelMemoryBuffer();

            _reader = new ChannelMemoryReader(_buffer.ConsumerPartitionGroup);
            _reader.FrameRead += OnFrameRead;
            _writer = new ChannelMemoryWriter(_buffer.ProducerPartitionGroup);
            
            if (Interlocked.Exchange(ref _isRunning, 1) != 0)
                return;

            _cleaner = new ChannelMemoryCleaner(_reader.Partitions);

            if (!manualPooling)
            {
                _consumerThread = new Thread(InboundReadThread) { IsBackground = true };
                _consumerThread.Start();
            }
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
            if(_isRunning == 0)
                throw new InvalidOperationException();

            return _reader.TryReadFrameBatch();
        }
        
        private void OnFrameRead(FrameBlock frame, bool endOfBatch)
        {
            if (frame.DataLength < sizeof(int))
                throw new InvalidOperationException($"Invalid frame length DataLength: {frame.DataLength} FrameLength: {frame.FrameLength}");

            var messageLength = Unsafe.ReadUnaligned<int>(frame.DataPosition);
            MessageReceived?.Invoke(new ReadOnlySpan<byte>(frame.DataPosition + sizeof(int), messageLength));
        }
        
        public void Stop()
        {
            if (Interlocked.Exchange(ref _isRunning, 0) != 1)
                return;

            _cleaner.Dispose();
            _buffer.Dispose();
            
            _consumerThread?.Join();
        }
    }
}
