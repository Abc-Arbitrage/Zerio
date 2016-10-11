namespace Abc.Zerio.Serialization
{
    public interface IMessageAllocator<out T> : IMessageAllocator
        where T : new()
    {
        new T Allocate();
    }
}
