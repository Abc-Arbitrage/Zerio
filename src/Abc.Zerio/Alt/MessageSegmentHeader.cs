using System.Runtime.InteropServices;

namespace Abc.Zerio.Alt
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
    internal readonly struct MessageSegmentHeader
    {
        public const int Size = 4;

        public readonly byte HasMore;
#pragma warning disable 414
        private readonly byte _reserved;
#pragma warning restore 414
        public readonly ushort Length;

        public MessageSegmentHeader(ushort length, bool hasMore)
        {
            Length = length;
            HasMore = hasMore ? (byte)1 : (byte)0;
            _reserved = 0;
        }

        
    }
}
