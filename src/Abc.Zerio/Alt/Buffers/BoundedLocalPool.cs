using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    public class BoundedLocalPool<T>
    {
        private readonly int _capacity;
        private readonly T[] _items;
        private int _mask;

        // long is enough for 68 years at int.Max operations per second.
        // [... head<- items -> tail ...]
        private long _tail;
        private long _head;

        private SpinLock _spinLock = new SpinLock(false);

        public BoundedLocalPool(int capacity)
        {
            if (!Utils.IsPowerOfTwo(capacity))
                throw new ArgumentException("Capacity must be a power of 2");
            _capacity = capacity;
            _items = new T[_capacity];
            _mask = _capacity - 1;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(Volatile.Read(ref _tail) - Volatile.Read(ref _head));
        }

        /// <summary>
        /// Called by a single <see cref="Poller"/> thread. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T item)
        {
            if (Count == _capacity)
                ThrowExceededCapacity();
            var idx = (int)(_tail & _mask);
            _items[idx] = item;
            Volatile.Write(ref _tail, Volatile.Read(ref _tail) + 1);
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
        public bool TryPop(out T item)
        {
            item = default;
            if (Count == 0)
                return false;

            var taken = false;
            try
            {
                _spinLock.Enter(ref taken);

                if (Count == 0)
                    return false;

                var idx = (int)(_head & _mask);
                item = _items[idx];
                Volatile.Write(ref _head, Volatile.Read(ref _head) + 1);
            }
            finally
            {
                if (taken)
                    _spinLock.Exit(useMemoryBarrier: false);
            }

            return false;
        }
    }
}
