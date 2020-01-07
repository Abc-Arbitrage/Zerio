using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Alt.Buffers
{
    internal unsafe struct RioSegment
    {
        public readonly byte* Pointer;
        public RIO_BUF RioBuf;
        private readonly BufferSegmentId _id;

        public RioSegment(byte* pointer, RIO_BUF rioBuf, BufferSegmentId id)
        {
            Pointer = pointer;
            RioBuf = rioBuf;
            _id = id;
        }

        public BufferSegmentId Id => _id;

        public int Length => RioBuf.Length;

        public Span<byte> Span => new Span<byte>(Pointer, RioBuf.Length);
    }
}
