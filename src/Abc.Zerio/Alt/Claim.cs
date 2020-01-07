using System;
using System.Runtime.CompilerServices;
using Abc.Zerio.Alt.Buffers;

namespace Abc.Zerio.Alt
{
    public readonly struct Claim
    {
        private readonly RioSegment _rioSegment;
        private readonly Session _session;

        internal Claim(RioSegment rioSegment, Session session)
        {
            _rioSegment = rioSegment;
            _session = session;
        }

        public Span<byte> Span => _rioSegment.Span.Slice(MessageSegmentHeader.Size);

        public void Commit(int length, bool hasMore)
        {
            if ((uint)length > _rioSegment.Length - MessageSegmentHeader.Size)
                throw new IndexOutOfRangeException();
            Unsafe.WriteUnaligned(ref _rioSegment.Span[0], new MessageSegmentHeader(checked((ushort)length), hasMore));
            var copy = _rioSegment;
            copy.RioBuf.Length = MessageSegmentHeader.Size + length;
            _session.Commit(copy);
        }
    }
}
