using System;
using System.IO;
using Abc.Zerio.Framing;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;

namespace Abc.Zerio.Server.Serializers
{
    public class PlaceOrderMessageSerializer : IBinaryMessageSerializer
    {
        private const byte _version = 1;

        public void Serialize(object message, UnsafeBinaryWriter binaryWriter)
        {
            binaryWriter.Write(_version);

            var placeOrderMessage = (PlaceOrderMessage)message;
            binaryWriter.Write(placeOrderMessage.Id);
            binaryWriter.Write(placeOrderMessage.InstrumentId);
            binaryWriter.Write(placeOrderMessage.Price);
            binaryWriter.Write(placeOrderMessage.Quantity);
            binaryWriter.Write((byte)placeOrderMessage.Side);
        }

        public void Deserialize(object message, UnsafeBinaryReader binaryReader)
        {
            var version = binaryReader.ReadByte();
            if (version != _version)
                throw new InvalidOperationException($"version mismatch; expected:{_version} received:{version}");

            var placeOrderMessage = (PlaceOrderMessage)message;
            placeOrderMessage.Id = binaryReader.ReadInt64();
            placeOrderMessage.InstrumentId = binaryReader.ReadInt32();
            placeOrderMessage.Price = binaryReader.ReadDouble();
            placeOrderMessage.Quantity = binaryReader.ReadDouble();
            placeOrderMessage.Side = (OrderSide)binaryReader.ReadByte();
        }
    }
}
