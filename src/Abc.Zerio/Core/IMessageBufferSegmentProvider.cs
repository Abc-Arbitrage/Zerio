using Abc.Zerio.Framing;

namespace Abc.Zerio.Core
{
    public interface IMessageBufferSegmentProvider : IBufferSegmentProvider
    {
        void SetMessageLength(int messageLength);
    }
}
