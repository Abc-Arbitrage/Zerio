using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Alt.Buffers
{
    internal unsafe struct RioSegment
    {
        private RegisteredBuffer _buffer;
        public readonly byte* Pointer;
        public RIO_BUF RioBuf;
        private readonly BufferSegmentId _id;

        public RioSegment(RegisteredBuffer buffer, byte* pointer, RIO_BUF rioBuf, BufferSegmentId id)
        {
            _buffer = buffer;
            Pointer = pointer;
            RioBuf = rioBuf;
            _id = id;
        }

        public BufferSegmentId Id => _id;

        public int Length => RioBuf.Length;

        public Span<byte> Span => new Span<byte>(Pointer, RioBuf.Length);

        public Memory<byte> Memory => _buffer.Memory.Slice((int)((long)Pointer - (long)_buffer.Start), _buffer.SegmentLength);
    }
}
