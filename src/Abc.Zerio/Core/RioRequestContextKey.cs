using System.Runtime.InteropServices;

namespace Abc.Zerio.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public unsafe struct RioRequestContextKey
    {
        [FieldOffset(0)]
        public readonly int BufferId;

        [FieldOffset(4)]
        public readonly RequestType RequestType;

        public RioRequestContextKey(int bufferId, RequestType requestType)
            : this()
        {
            BufferId = bufferId;
            RequestType = requestType;
        }

        public long ToRioRequestCorrelationId()
        {
            var @this = this;
            return *(long*)&@this;
        }

        public RioRequestContextKey(long rioRequestCorrelationId)
            : this()
        {
            this = *(RioRequestContextKey*)&rioRequestCorrelationId;
        }
    }
}
