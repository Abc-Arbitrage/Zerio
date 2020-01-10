using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    public class BoundedLocalPool<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly int _capacity;
        
        public BoundedLocalPool(int capacity)
        {
            if (!Utils.IsPowerOfTwo(capacity))
                throw new ArgumentException("Capacity must be a power of 2");
            _capacity = capacity;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _queue.Count; // (Volatile.Read(ref _tail) - Volatile.Read(ref _head));
        }

        /// <summary>
        /// Called by a single <see cref="Poller"/> thread. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (Count >= _capacity)
                ThrowExceededCapacity();
            _queue.Enqueue(item);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowExceededCapacity()
        {
            throw new InvalidOperationException("Exceeded capacity");
        }

        /// <summary>
        /// Only multiple sends per <see cref="Session"/> could compete. Single send should never spin.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRent(out T item)
        {
            return _queue.TryDequeue(out item);
        }
    }
}
