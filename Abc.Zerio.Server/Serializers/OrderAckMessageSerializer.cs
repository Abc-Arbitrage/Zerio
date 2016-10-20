using System;
using Abc.Zerio.Framing;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;

namespace Abc.Zerio.Server.Serializers
{
    public class OrderAckMessageSerializer : IBinaryMessageSerializer
    {
        private const byte _version = 1;

        public void Serialize(object message, UnsafeBinaryWriter binaryWriter)
        {
            binaryWriter.Write(_version);

            var orderAckMessage = (OrderAckMessage)message;
            binaryWriter.Write(orderAckMessage.Id);
        }

        public void Deserialize(object message, UnsafeBinaryReader binaryReader)
        {
            var version = binaryReader.ReadByte();
            if (version != _version)
                throw new InvalidOperationException($"version mismatch; expected:{_version} received:{version}");

            var orderAckMessage = (OrderAckMessage)message;
            orderAckMessage.Id = binaryReader.ReadInt64();
        }
    }
}
