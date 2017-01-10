using System;
using Abc.Zerio.Framing;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;

namespace Abc.Zerio.Server.Serializers
{
    public class PlaceOrderMessageSerializer : BinaryMessageSerializer<PlaceOrderMessage>
    {
        private const byte _version = 1;

        public override void Serialize(PlaceOrderMessage message, UnsafeBinaryWriter binaryWriter)
        {
            binaryWriter.Write(_version);

            binaryWriter.Write(message.Id);
            binaryWriter.Write(message.InstrumentId);
            binaryWriter.Write(message.Price);
            binaryWriter.Write(message.Quantity);
            binaryWriter.Write((byte)message.Side);
        }

        public override void Deserialize(PlaceOrderMessage message, UnsafeBinaryReader binaryReader)
        {
            var version = binaryReader.ReadByte();
            if (version != _version)
                throw new InvalidOperationException($"version mismatch; expected:{_version} received:{version}");

            message.Id = binaryReader.ReadInt64();
            message.InstrumentId = binaryReader.ReadInt32();
            message.Price = binaryReader.ReadDouble();
            message.Quantity = binaryReader.ReadDouble();
            message.Side = (OrderSide)binaryReader.ReadByte();
        }
    }
}
