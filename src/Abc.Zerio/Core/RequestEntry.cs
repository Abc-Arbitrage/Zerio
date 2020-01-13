using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    [StructLayout(LayoutKind.Explicit, Size = sizeof(RequestType) + sizeof(int) + RIO_BUF.Size)]
    public unsafe struct RequestEntry : IRioBufferSegmentDescriptorContainer
    {
        [FieldOffset(0)]
        public RequestType Type;

        [FieldOffset(4)]
        public int SessionId;

        [FieldOffset(8)]
        internal RIO_BUF RioBufferSegmentDescriptor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RIO_BUF* GetRioBufferDescriptor() => (RIO_BUF*)Unsafe.AsPointer(ref RioBufferSegmentDescriptor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetBufferSegmentStart() => (byte*)(GetRioBufferDescriptor() + 1);

        public void SetWriteRequest(int sessionId, ReadOnlySpan<byte> message)
        {
            Type = RequestType.Send;
            SessionId = sessionId;
            
            // The actual buffer segment is located right at the end of the current struct
            var bufferSegmentStart = GetBufferSegmentStart();

            Unsafe.Write(bufferSegmentStart, message.Length);
            message.CopyTo(new Span<byte>(bufferSegmentStart + sizeof(int), message.Length));

            RioBufferSegmentDescriptor.Length = sizeof(int) + message.Length;
        }

        public void Reset()
        {
            Type = RequestType.Undefined;
            RioBufferSegmentDescriptor.Length = default;
            SessionId = default;
        }
    }
}
