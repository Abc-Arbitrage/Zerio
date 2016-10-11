using System.IO;

namespace Abc.Zerio.Serialization
{
    public interface IBinaryMessageSerializer
    {
        void Serialize(object message, BinaryWriter binaryWriter);
        void Deserialize(object message, BinaryReader binaryReader);
    }
}
