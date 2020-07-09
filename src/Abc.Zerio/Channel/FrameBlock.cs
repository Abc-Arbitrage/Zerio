using System.Threading;

namespace Abc.Zerio.Channel
{
    internal unsafe struct FrameBlock
    {
        public static FrameBlock Empty { get; } = new FrameBlock();

        public FrameBlock(byte* framePosition, long frameLength)
        {
            FramePosition = framePosition;
            FrameLength = frameLength;
        }

        public byte* FramePosition { get; }
        public byte* DataPosition => FramePosition + _dataOffset;

        public long FrameLength { get; }
        public long DataLength => FrameLength - _dataOffset;

        public bool IsEmpty => FramePosition == null;

        // Markers must be less than int.MinValue, as negative sizes are used to skip invalid messages
        public const long EndOfPartitionMarker = long.MinValue;
        public const long EndOfPartitionMarkerSize = sizeof(long);

        private const long _dataOffset = sizeof(long); // space needed for the frame length prefix

        public static long GetFrameLength(long dataLength)
        {
            var frameLength = dataLength + _dataOffset;

            // Should be a multiple of 8 to ensure that frames are aligned and that the length values are aligned
            return (frameLength + 7) & ~7;
        }

        public void Publish(bool isValid)
        {
            if (IsEmpty)
                return;

            Volatile.Write(ref *(long*)FramePosition, isValid ? FrameLength : -FrameLength);
        }
    }
}
