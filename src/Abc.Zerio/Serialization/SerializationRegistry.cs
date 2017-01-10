using System.Collections.Generic;
using System.Text;

namespace Abc.Zerio.Serialization
{
    public class SerializationRegistry
    {
        private readonly List<SerializationMapping> _mappings = new List<SerializationMapping>();

        public Encoding Encoding { get; private set; }
        public IEnumerable<SerializationMapping> Mappings => _mappings;

        public SerializationRegistry(Encoding encoding)
        {
            Encoding = encoding;
        }

        public SerializationRegistry Register<TMessage, TSerializer>(IMessageAllocator allocator = null, IMessageReleaser releaser = null)
            where TMessage : new()
            where TSerializer : IBinaryMessageSerializer
        {
            _mappings.Add(new SerializationMapping
            {
                Allocator = allocator ?? new HeapAllocator<TMessage>(),
                Releaser = releaser,
                MessageSerializerType = typeof(TSerializer),
                MessageType = typeof(TMessage),
            });

            return this;
        }
    }
}
