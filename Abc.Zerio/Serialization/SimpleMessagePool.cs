using System.Collections.Concurrent;

namespace Abc.Zerio.Serialization
{
    public class SimpleMessagePool<T> : IMessageAllocator<T>, IMessageReleaser<T>
        where T : new()
    {
        private readonly ConcurrentQueue<T> _pool = new ConcurrentQueue<T>();

        public SimpleMessagePool(int capacity)
        {
            for (var i = 0; i < capacity; i++)
            {
                _pool.Enqueue(new T());
            }
        }

        public void Release(T item)
        {
            _pool.Enqueue(item);
        }

        public T Allocate()
        {
            T result;
            _pool.TryDequeue(out result);
            return result;
        }

        object IMessageAllocator.Allocate()
        {
            return Allocate();
        }

        void IMessageReleaser.Release(object item)
        {
            Release((T)item);
        }
    }
}
