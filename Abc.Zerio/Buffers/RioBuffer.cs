using Abc.Zerio.Interop;

namespace Abc.Zerio.Buffers
{
    public unsafe class RioBuffer
    {
        internal readonly RIO_BUF* BufferDescriptor;

        public readonly int Id;
        public readonly byte* Data;
        public readonly int Length;

        public int DataLength { get { return BufferDescriptor->Length; } set { BufferDescriptor->Length = value; } }

        public RioBuffer(int id, byte* dataPointer, RIO_BUF* bufferDescriptor, int length)
        {
            Id = id;
            Data = dataPointer;
            BufferDescriptor = bufferDescriptor;
            Length = length;
        }

        public void Reset()
        {
            BufferDescriptor->Length = Length;
        }
    }
}
