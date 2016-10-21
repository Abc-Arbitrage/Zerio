using Abc.Zerio.Framing;

namespace Abc.Zerio.Serialization
{
    public interface IBinaryMessageSerializer<in T> : IBinaryMessageSerializer
    {
        void Serialize(T message, UnsafeBinaryWriter binaryWriter);
        void Deserialize(T message, UnsafeBinaryReader binaryReader);
    }
}
