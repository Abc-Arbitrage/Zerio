namespace Abc.Zerio.Serialization
{
    public interface IMessageAllocator<out T> : IMessageAllocator
    {
        new T Allocate();
    }
}