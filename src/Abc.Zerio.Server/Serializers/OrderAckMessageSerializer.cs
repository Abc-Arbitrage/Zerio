using System;
using Abc.Zerio.Framing;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;

namespace Abc.Zerio.Server.Serializers
{
    public class OrderAckMessageSerializer : BinaryMessageSerializer<OrderAckMessage>
    {
        private const byte _version = 1;

        public override void Serialize(OrderAckMessage message, UnsafeBinaryWriter binaryWriter)
        {
            binaryWriter.Write(_version);
            binaryWriter.Write(message.Id);
        }

        public override void Deserialize(OrderAckMessage message, UnsafeBinaryReader binaryReader)
        {
            var version = binaryReader.ReadByte();
            if (version != _version)
                throw new InvalidOperationException($"version mismatch; expected:{_version} received:{version}");

            message.Id = binaryReader.ReadInt64();
        }
    }
}
