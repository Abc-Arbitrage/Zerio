using Abc.Zerio.Framing;

namespace Abc.Zerio.Serialization
{
    public abstract class BinaryMessageSerializer<T> : IBinaryMessageSerializer
    {
        void IBinaryMessageSerializer.Serialize(object message, UnsafeBinaryWriter binaryWriter)
        {
            Serialize((T)message, binaryWriter);
        }

        void IBinaryMessageSerializer.Deserialize(object message, UnsafeBinaryReader binaryReader)
        {
            Deserialize((T)message, binaryReader);
        }

        public abstract void Serialize(T message, UnsafeBinaryWriter binaryWriter);

        public abstract void Deserialize(T message, UnsafeBinaryReader binaryReader);
    }
}
