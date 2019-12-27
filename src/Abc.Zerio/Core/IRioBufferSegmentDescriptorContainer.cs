using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal unsafe interface IRioBufferSegmentDescriptorContainer
    {
        RIO_BUF* GetRioBufferDescriptor();
    }
}
