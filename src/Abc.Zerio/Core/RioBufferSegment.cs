using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    [StructLayout(LayoutKind.Explicit, Size = RIO_BUF.Size)]
    public unsafe struct RioBufferSegment : IRioBufferSegmentDescriptorContainer
    {
        [FieldOffset(0)]
        internal RIO_BUF RioBufferSegmentDescriptor;
        
        public RIO_BUF* GetRioBufferDescriptor() => (RIO_BUF*)Unsafe.AsPointer(ref RioBufferSegmentDescriptor);
        
        public byte* GetBufferSegmentStart()
        {
            // The start of the buffer segment is right behind the end of the current struct
            return (byte*)(GetRioBufferDescriptor() + 1);
        }
    }
}
