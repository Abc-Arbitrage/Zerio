using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Abc.Zerio.Interop;
using HdrHistogram;

namespace Abc.Zerio.Channel
{
    public class RegisteredMemoryChannel
    {
        private readonly int _maxFrameBatchSize;
        private readonly ManyToOneRingBuffer _ringBuffer;
        private readonly LongHistogram _histogram;

        public event ChannelFrameReadDelegate FrameRead;

        public unsafe RegisteredMemoryChannel(int bufferLength, int maxFrameBatchSize = int.MaxValue)
        {
            _maxFrameBatchSize = maxFrameBatchSize;
            _ringBuffer = new ManyToOneRingBuffer(bufferLength);
            _ringBuffer.FrameRead += (frame, endOfBatch, token) =>
            {
                if (!frame.IsEmpty && frame.DataLength > 11)
                {
                    var start = Unsafe.ReadUnaligned<long>(frame.DataStart);
                    var rrt = Stopwatch.GetTimestamp() - start;
                    var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);
            
                    _histogram.RecordValue(micros);
                }
                
                FrameRead?.Invoke(frame, endOfBatch, token);
            };
               
            _histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
        }

        internal RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        {
            return _ringBuffer.CreateBufferSegmentDescriptor(frame);
        }

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            // var spinWait = new SpinWait();

            while (!_ringBuffer.Write(messageBytes))
            {
                // spinWait.SpinOnce();
            }
        }

        public bool TryPoll()
        {
            return _ringBuffer.Read(_maxFrameBatchSize) > 0;
        }
   
        public void Stop()
        {
            _ringBuffer.Dispose();
        }

        public void CompleteSend(CompletionToken completionToken)
        {
            _ringBuffer.CompleteRead(completionToken);
        }

        public void DisplayStats()
        {
            _histogram.OutputPercentileDistribution(Console.Out, 1);
        }

        public void ResetStats()
        {
            _histogram.Reset();
        }
    }
}
