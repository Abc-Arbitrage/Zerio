namespace Abc.Zerio.Channel
{
    public unsafe struct ChannelFrame
    {
        public byte* DataPosition { get; }
        public long DataLength { get; }

        public ChannelFrame(byte* dataPosition, long dataLength)
        {
            DataPosition = dataPosition;
            DataLength = dataLength;
        }
    }
}
