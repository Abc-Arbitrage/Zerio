using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Channel
{
    public class RegisteredMemoryChannel
    {
        private readonly ManyToOneRingBuffer _ringBuffer;

        public event ChannelFrameReadDelegate FrameRead;

        public RegisteredMemoryChannel(int bufferLength)
        {
            _ringBuffer = new ManyToOneRingBuffer(bufferLength);
            _ringBuffer.FrameRead += (frame, endOfBatch, token) => FrameRead?.Invoke(frame, endOfBatch, token);
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
            return _ringBuffer.Read() > 0;
        }

        public void Stop()
        {
            _ringBuffer.Dispose();
        }

        public void CompleteSend(CompletionToken completionToken)
        {
            _ringBuffer.CompleteRead(completionToken);
        }
    }
}
