using System;
using System.Threading;
using Abc.Zerio.Interop;
using HdrHistogram;

namespace Abc.Zerio.Channel
{
    public class RegisteredMemoryChannel
    {
        private readonly ManyToOneRingBuffer _ringBuffer;
        private readonly LongHistogram _histogram;
        
        public event ChannelFrameReadDelegate FrameRead;
        
        public RegisteredMemoryChannel(int bufferLength)
        {
            _ringBuffer = new ManyToOneRingBuffer(bufferLength);
            _ringBuffer.FrameRead += (frame, token) => FrameRead?.Invoke(frame, token);
            
            _histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
        }

        internal RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        {
            return _ringBuffer.CreateBufferSegmentDescriptor(frame);
        }

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
            return _ringBuffer.Read() > 0;
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

        public void CompleteSend(SendCompletionToken sendCompletionToken)
        {
            _ringBuffer.CompleteSend(sendCompletionToken);
        }
    }
}
