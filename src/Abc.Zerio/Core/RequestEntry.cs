using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    [StructLayout(LayoutKind.Explicit, Size = sizeof(RequestType) + sizeof(int) + sizeof(int) + RIO_BUF.Size)]
    public unsafe struct RequestEntry : IRioBufferSegmentDescriptorContainer
    {
        [FieldOffset(0)]
        public RequestType Type;

        [FieldOffset(1)]
        public int SessionId;

        [FieldOffset(5)]
        public int BufferSegmentId;

        [FieldOffset(9)]
        internal RIO_BUF RioBufferSegmentDescriptor;

        public RIO_BUF* GetRioBufferDescriptor() => (RIO_BUF*)Unsafe.AsPointer(ref RioBufferSegmentDescriptor);
        
        public byte* GetBufferSegmentStart() => (byte*)(GetRioBufferDescriptor() + 1);

        public void SetWriteRequest(int sessionId, ReadOnlySpan<byte> message)
        {
            Type = RequestType.Send;
            SessionId = sessionId;

            // The actual buffer segment is located right at the end of the current struct
            var bufferSegmentStart = GetBufferSegmentStart();

            Unsafe.WriteUnaligned(ref bufferSegmentStart[0], message.Length);
            message.CopyTo(new Span<byte>(bufferSegmentStart + sizeof(int), message.Length));

            RioBufferSegmentDescriptor.Length = sizeof(int) + message.Length;
        }

        public void SetReadRequest(int sessionId, int bufferSegmentId)
        {
            Type = RequestType.Receive;
            SessionId = sessionId;
            BufferSegmentId = bufferSegmentId;
        }

        public void Reset()
        {
            Type = RequestType.Undefined;
            RioBufferSegmentDescriptor.Length = default;
            SessionId = default;
            BufferSegmentId = default;
        }
    }
}
