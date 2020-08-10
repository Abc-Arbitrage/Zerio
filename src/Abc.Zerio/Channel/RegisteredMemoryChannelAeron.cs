using System;
using System.Diagnostics;
using System.Threading;
using HdrHistogram;

namespace Abc.Zerio.Channel
{
    public unsafe class RegisteredMemoryChannelAeron
    {
        private readonly ManyToOneRingBuffer _ringBuffer;
        private readonly MessageHandler _messageHandler;
        private readonly LongHistogram _histogram;

        public event Action<ChannelFrame> FrameRead;
        public event Action EndOfBatch;

        public RegisteredMemoryChannelAeron()
        {
            _ringBuffer = new ManyToOneRingBuffer(4_000_000);
            _messageHandler = ProcessMessage;
            _histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
        }

        private void ProcessMessage(int msgTypeId, byte* buffer, int index, int length)
        {
            var pointer = buffer + index;

            var start = ManyToOneRingBuffer.GetLong(buffer, index);
            var rrt = Stopwatch.GetTimestamp() - start;
            var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);

            _histogram.RecordValue(micros);

            FrameRead?.Invoke(new ChannelFrame(pointer, length));
        }

        // internal RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        // {
        //     return _buffer.CreateBufferSegmentDescriptor(frame);
        // }

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            var spinWait = new SpinWait();

            while (!_ringBuffer.Write(messageBytes))
            {
                spinWait.SpinOnce();
            }
        }

        public bool TryPoll()
        {
            if (_ringBuffer.Read(_messageHandler) > 0)
            {
                EndOfBatch?.Invoke();
                return true;
            }

            return false;
        }

        public void DisplayStats()
        {
            _histogram.OutputPercentileDistribution(Console.Out, 1);
        }

        public void Stop()
        {
            _ringBuffer.Dispose();
        }

        public void ResetStats()
        {
            _histogram.Reset();
        }
    }
}
