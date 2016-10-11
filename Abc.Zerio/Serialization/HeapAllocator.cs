namespace Abc.Zerio.Serialization
{
    public class HeapAllocator<T> : IMessageAllocator<T>
        where T : new()
    {
        public T Allocate()
        {
            return new T();
        }

        object IMessageAllocator.Allocate()
        {
            return Allocate();
        }
    }
}
