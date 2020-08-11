namespace Abc.Zerio.Channel
{
    public unsafe struct ChannelFrame
    {
        public const int HeaderLength = sizeof(int) * 2;
        public const int Alignment = HeaderLength;
        public const int PaddingFrame = -1;
        public const int DataFrame = 1;
        
        public readonly byte* FrameStart;
        public readonly int FrameLength;
        public readonly int DataLength;
        public byte* DataStart => FrameStart + HeaderLength;
        public bool IsEmpty => DataLength == 0;

        public ChannelFrame(byte* frameStart, int frameLength, int dataLength)
        {
            FrameStart = frameStart;
            FrameLength = frameLength;
            DataLength = dataLength;
        }

        public static int RecordLength(long header) => (int)header;
        public static long MakeHeader(int length, int frameTypeId) => ((frameTypeId & 0xFFFFFFFFL) << 32) | (length & 0xFFFFFFFFL);
        public static int EncodedDataOffset(int recordOffset) => recordOffset + HeaderLength;
        public static int LengthOffset(int recordOffset) => recordOffset;
        public static int FrameTypeId(long header) => (int) (long) ((ulong) header >> 32);
    }
}
