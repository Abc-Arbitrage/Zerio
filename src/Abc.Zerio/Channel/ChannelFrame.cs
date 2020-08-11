using Adaptive.Agrona.Concurrent.Broadcast;

namespace Abc.Zerio.Channel
{
    public unsafe struct ChannelFrame
    {
        public readonly byte* FrameStart;
        public readonly int FrameLength;
        public readonly int DataLength;
        public byte* DataStart => FrameStart + RecordDescriptor.HeaderLength;
        public bool IsEmpty => DataLength == 0;

        public ChannelFrame(byte* frameStart, int frameLength, int dataLength)
        {
            FrameStart = frameStart;
            FrameLength =  frameLength;
            DataLength = dataLength;
        }
    }
}
