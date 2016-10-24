using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Abc.Zerio.Framing;

namespace Abc.Zerio.Tests.Utils
{
    internal unsafe class BufferSegmentProvider : IBufferSegmentProvider, IDisposable
    {
        private readonly byte[] _buffer;
        private readonly Queue<BufferSegment> _segments = new Queue<BufferSegment>();

        private GCHandle _handle;

        public BufferSegmentProvider(int segmentCount, int segmentSize)
        {
            _buffer = new byte[segmentCount * segmentSize];
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            var pointer = (byte*)_handle.AddrOfPinnedObject();
            var lastSegmentPointer = pointer + _buffer.Length - segmentSize;

            if (segmentSize == 0)
            {
                AllocateEmptySegments(pointer, segmentCount);
                return;
            }

            while (pointer <= lastSegmentPointer)
            {
                _segments.Enqueue(new BufferSegment(pointer, segmentSize));
                pointer += segmentSize;
            }
        }

        private void AllocateEmptySegments(byte* pointer, int segmentCount)
        {
            for (var i = 0; i < segmentCount; i++)
            {
                _segments.Enqueue(new BufferSegment(pointer, 0));
            }
        }

        public byte* GetUnderlyingBufferPointer()
        {
            return (byte*)_handle.AddrOfPinnedObject();
        }

        public BinaryReader GetBinaryReader(Encoding encoding)
        {
            var memoryStream = new MemoryStream(_buffer);
            return new BinaryReader(memoryStream, encoding);
        }

        public BinaryWriter GetBinaryWriter(Encoding encoding)
        {
            var memoryStream = new MemoryStream(_buffer);
            return new BinaryWriter(memoryStream, encoding);
        }

        public BufferSegment GetBufferSegment()
        {
            return _segments.Dequeue();
        }

        public void Dispose()
        {
            _handle.Free();

            GC.SuppressFinalize(this);
        }

        ~BufferSegmentProvider()
        {
            _handle.Free();
        }

        public List<BufferSegment> GetBufferSegments()
        {
            return new List<BufferSegment>(_segments);
        }
    }
}
