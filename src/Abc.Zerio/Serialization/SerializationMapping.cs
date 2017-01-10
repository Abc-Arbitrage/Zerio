using System;

namespace Abc.Zerio.Serialization
{
    public class SerializationMapping
    {
        public Type MessageType;
        public Type MessageSerializerType;
        public IMessageAllocator Allocator { get; set; }
        public IMessageReleaser Releaser { get; set; }
    }
}
