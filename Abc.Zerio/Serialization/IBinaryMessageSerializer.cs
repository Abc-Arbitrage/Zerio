using Abc.Zerio.Framing;

namespace Abc.Zerio.Serialization
{
    public interface IBinaryMessageSerializer
    {
        void Serialize(object message, UnsafeBinaryWriter binaryWriter);
        void Deserialize(object message, UnsafeBinaryReader binaryReader);
    }
}
