using System;
using System.Collections.Generic;
using System.Text;
using Abc.Zerio.Core;
using Abc.Zerio.Framing;

namespace Abc.Zerio.Serialization
{
    public class SerializationEngine
    {
        private readonly Dictionary<Type, MessageSerializationEngine> _messageEnginesByType = new Dictionary<Type, MessageSerializationEngine>();
        private readonly Dictionary<MessageTypeId, MessageSerializationEngine> _messageEnginesByTypeId = new Dictionary<MessageTypeId, MessageSerializationEngine>();

        public readonly Encoding Encoding;

        public SerializationEngine(SerializationRegistry registry)
        {
            Encoding = registry.Encoding;

            CreateMessageSerializationEngines(registry);
        }

        private void CreateMessageSerializationEngines(SerializationRegistry registry)
        {
            foreach (var mapping in registry.Mappings)
            {
                var messageEngine = MessageSerializationEngine.Create(mapping);

                _messageEnginesByType.Add(messageEngine.MessageType, messageEngine);
                _messageEnginesByTypeId.Add(messageEngine.MessageTypeId, messageEngine);
            }
        }

        public void SerializeWithLengthPrefix(object message, IMessageBufferSegmentProvider segmentProvider, UnsafeBinaryWriter binaryWriter)
        {
            var messageType = message.GetType();
            var serializationEngine = _messageEnginesByType[messageType];

            binaryWriter.SetBufferSegmentProvider(segmentProvider);

            var lengthPrefixPosition = binaryWriter.GetPosition();
            binaryWriter.Write(0); // length prefix

            binaryWriter.Write((uint)serializationEngine.MessageTypeId);

            serializationEngine.Serializer.Serialize(message, binaryWriter);

            var messageLength = (int)binaryWriter.GetLength();

            binaryWriter.SetPosition(lengthPrefixPosition);
            binaryWriter.Write(messageLength - sizeof(int));

            segmentProvider.SetMessageLength(messageLength);
        }

        public ReleasableMessage DeserializeWithLengthPrefix(List<BufferSegment> framedMessage, UnsafeBinaryReader binaryReader)
        {
            binaryReader.SetBuffers(framedMessage);
            var messageTypeId = new MessageTypeId(binaryReader.ReadUInt32());

            var serializationEngine = _messageEnginesByTypeId[messageTypeId];
            var message = serializationEngine.Allocator.Allocate();
            serializationEngine.Serializer.Deserialize(message, binaryReader);

            return new ReleasableMessage(messageTypeId, message, serializationEngine.Releaser);
        }

        public struct ReleasableMessage
        {
            public readonly MessageTypeId MessageTypeId;
            public readonly object Message;
            public readonly IMessageReleaser Releaser;

            public ReleasableMessage(MessageTypeId messageTypeId, object message, IMessageReleaser releaser)
            {
                MessageTypeId = messageTypeId;
                Message = message;
                Releaser = releaser;
            }
        }

        private class MessageSerializationEngine
        {
            public readonly MessageTypeId MessageTypeId;
            public readonly Type MessageType;
            public readonly IBinaryMessageSerializer Serializer;
            public readonly IMessageAllocator Allocator;
            public readonly IMessageReleaser Releaser;

            private MessageSerializationEngine(MessageTypeId messageTypeId, Type messageType, IBinaryMessageSerializer messageSerializer, IMessageAllocator allocator, IMessageReleaser releaser)
            {
                MessageTypeId = messageTypeId;
                MessageType = messageType;
                Serializer = messageSerializer;
                Allocator = allocator;
                Releaser = releaser;
            }

            public static MessageSerializationEngine Create(SerializationMapping serializationMapping)
            {
                if (serializationMapping == null)
                    throw new ArgumentNullException(nameof(serializationMapping));

                var messageType = serializationMapping.MessageType;
                var messageTypeId = MessageTypeId.Get(messageType);
                var messageSerializer = (IBinaryMessageSerializer)Activator.CreateInstance(serializationMapping.MessageSerializerType);
                var messageEngine = new MessageSerializationEngine(messageTypeId, messageType, messageSerializer, serializationMapping.Allocator, serializationMapping.Releaser);

                return messageEngine;
            }
        }
    }
}
