using System;
using System.Collections.Generic;
using System.Text;
using Abc.Zerio.Core;
using Abc.Zerio.Framing;

namespace Abc.Zerio.Serialization
{
    public unsafe class SerializationEngine
    {
        private readonly Dictionary<Type, MessageSerializationEngine> _messageEnginesByType = new Dictionary<Type, MessageSerializationEngine>();
        private readonly Dictionary<uint, MessageSerializationEngine> _messageEnginesByTypeId = new Dictionary<uint, MessageSerializationEngine>();

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

            binaryWriter.Write(serializationEngine.MessageTypeId);

            serializationEngine.Serializer.Serialize(message, binaryWriter);

            var messageLength = (int)binaryWriter.GetLength();

            binaryWriter.SetPosition(lengthPrefixPosition);
            binaryWriter.Write(messageLength - sizeof(int));

            segmentProvider.SetMessageLength(messageLength);
        }

        public ReleasableMessage DeserializeWithLengthPrefix(List<BufferSegment> framedMessage, UnsafeBinaryReader binaryReader)
        {
            binaryReader.SetBuffers(framedMessage);
            var messageTypeId = binaryReader.ReadUInt32();

            var serializationEngine = _messageEnginesByTypeId[messageTypeId];
            var message = serializationEngine.Allocator.Allocate();
            serializationEngine.Serializer.Deserialize(message, binaryReader);

            return new ReleasableMessage(message, serializationEngine.Releaser);
        }

        public struct ReleasableMessage
        {
            public readonly object Message;
            public readonly IMessageReleaser Releaser;

            public ReleasableMessage(object message, IMessageReleaser releaser)
            {
                Message = message;
                Releaser = releaser;
            }
        }

        private class MessageSerializationEngine
        {
            public readonly uint MessageTypeId;
            public readonly Type MessageType;
            public readonly IBinaryMessageSerializer Serializer;
            public readonly IMessageAllocator Allocator;
            public readonly IMessageReleaser Releaser;

            private MessageSerializationEngine(uint messageTypeId, Type messageType, IBinaryMessageSerializer messageSerializer, IMessageAllocator allocator, IMessageReleaser releaser)
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
                var messageTypeId = CreateTypeId(messageType);
                var messageSerializer = (IBinaryMessageSerializer)Activator.CreateInstance(serializationMapping.MessageSerializerType);
                var messageEngine = new MessageSerializationEngine(messageTypeId, messageType, messageSerializer, serializationMapping.Allocator, serializationMapping.Releaser);

                return messageEngine;
            }

            private static uint CreateTypeId(Type messageType)
            {
                // the type id is a crc32 of the type full name; we should allow a custom one by attribute usage

                const uint poly = 0xedb88320;
                const int tableLength = 256;

                var table = stackalloc uint[tableLength];

                for (uint i = 0; i < tableLength; ++i)
                {
                    var temp = i;
                    for (var j = 8; j > 0; --j)
                    {
                        if ((temp & 1) == 1)
                            temp = (temp >> 1) ^ poly;
                        else
                            temp >>= 1;
                    }
                    table[i] = temp;
                }

                var messageTypeFullName = messageType.FullName;
                var fullNameLength = messageTypeFullName.Length;

                var bytes = stackalloc byte[fullNameLength];

                fixed (char* pChar = messageTypeFullName)
                {
                    var length = Encoding.ASCII.GetBytes(pChar, fullNameLength, bytes, fullNameLength);

                    var crc = 0xffffffff;
                    for (var i = 0; i < length; ++i)
                    {
                        var index = (byte)((crc & 0xff) ^ bytes[i]);
                        crc = (crc >> 8) ^ table[index];
                    }

                    return ~crc;
                }
            }
        }
    }
}
