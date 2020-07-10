using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Adaptive.Agrona;
using Adaptive.Agrona.Concurrent;
using Adaptive.Agrona.Concurrent.RingBuffer;
using HdrHistogram;

namespace Abc.Zerio.Channel
{
    public unsafe class RegisteredMemoryChannelAeron
    {
        private readonly UnsafeBuffer _writeBuffer = new UnsafeBuffer();
        private readonly ManyToOneRingBuffer _ringBuffer;
        private readonly MessageHandler _messageHandler;
        private readonly LongHistogram _histogram;

        public event Action<ChannelFrame> FrameRead;
        public event Action EndOfBatch;
        
        public RegisteredMemoryChannelAeron()
        {
            var bufferSize = BitUtil.FindNextPositivePowerOfTwo(4_000_000) + RingBufferDescriptor.TrailerLength;

            _ringBuffer = new ManyToOneRingBuffer(new UnsafeBuffer(new byte[bufferSize]));
            _messageHandler = ProcessMessage;
            _histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
        }

        private void ProcessMessage(int msgTypeId, IMutableDirectBuffer buffer, int index, int length)
        {
            var pointer = (byte*)buffer.BufferPointer + index;

            // Temporary:
            //var start = Unsafe.ReadUnaligned<long>(pointer - sizeof(long));
            var start = buffer.GetLong(index);
            var rrt = Stopwatch.GetTimestamp() - start;
            var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);
            
            _histogram.RecordValue(micros);
            
            FrameRead?.Invoke(new ChannelFrame(pointer, length));
        }

        public void CleanupPartitions()
        {
            //_reader.CleanupPartition();
        }

        // internal RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        // {
        //     return _buffer.CreateBufferSegmentDescriptor(frame);
        // }
        
        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            var spinWait = new SpinWait();

            fixed (byte* b = &messageBytes.GetPinnableReference())
            {
                _writeBuffer.Wrap(b, messageBytes.Length);
                _writeBuffer.PutLong(0, Stopwatch.GetTimestamp());
                
                while (!_ringBuffer.Write(1, _writeBuffer, 0, messageBytes.Length))
                {
                    spinWait.SpinOnce();
                }
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
            //_buffer.Dispose();
        }

        public void ResetStats()
        {
            _histogram.Reset();
        }
    }
}
