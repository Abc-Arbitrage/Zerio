using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    internal interface ISegmentPool : IDisposable
    {
        bool TryRent(out RioSegment segment);
        void Return(RioSegment segment);
        void AddBuffer(RegisteredBuffer buffer);
        RegisteredBuffer GetBuffer(int bufferId);
        int SegmentLength { get; }
    }

    internal class DefaultSegmentPool : CriticalFinalizerObject, ISegmentPool
    {
        private readonly List<RegisteredBuffer> _buffers = new List<RegisteredBuffer>(64);
        private volatile int _capacity;
        private readonly byte _poolId;

        private readonly CancellationToken _ct;
        private ConcurrentQueue<RioSegment> _queue;

        public DefaultSegmentPool(byte poolId, int segmentLength, CancellationToken ct = default)
        {
            _poolId = poolId;
            SegmentLength = segmentLength;
            _ct = ct;
            _queue = new ConcurrentQueue<RioSegment>();
        }

        public int SegmentLength { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRent(out RioSegment segment)
        {
            return _queue.TryDequeue(out segment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(RioSegment segment)
        {
            if (_poolId != segment.Id.PoolId)
                ThrowSegmentNotFromPool();

            var buffer = GetBuffer(segment.Id.BufferId);
            if (buffer.IsPooled)
            {
                // reset length
                segment.RioBuf.Length = SegmentLength;
                _queue.Enqueue(segment);
            }
            else
            {
                Interlocked.Decrement(ref _capacity);
                var remaining = Interlocked.Decrement(ref buffer.PooledSegmentCount);
                if (remaining == 0)
                {
                    buffer.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSegmentNotFromPool()
        {
            throw new InvalidOperationException("Segment not from pool");
        }

        public void AddBuffer(RegisteredBuffer buffer)
        {
            if (buffer.SegmentLength != SegmentLength)
                throw new InvalidOperationException("buffer.SegmentLength != SegmentLength");

            int idx = -1;
            lock (_buffers)
            {
                for (int i = 0; i < _buffers.Count; i++)
                {
                    if (_buffers[i] == null)
                    {
                        _buffers[i] = buffer;
                        idx = i;
                        break;
                    }
                }

                if (idx == -1)
                {
                    idx = _buffers.Count;
                    _buffers.Add(buffer);
                }
            }

            buffer.PoolId = _poolId;
            buffer.BufferId = idx;
            buffer.IsPooled = true;

            for (int i = 0; i < buffer.Count; i++)
            {
                _queue.Enqueue(buffer[i]);
            }

            Interlocked.Add(ref _capacity, buffer.Count);
        }

        // ReSharper disable once InconsistentlySynchronizedField
        public RegisteredBuffer GetBuffer(int bufferId) => _buffers[bufferId];

        public void Dispose()
        {
            foreach (var registeredBuffer in _buffers)
            {
                registeredBuffer.IsPooled = false;
            }

            // Wait until all buffers are returned.
            while (_capacity > 0)
            {
                Thread.Sleep(1);
            }
        }
    }
}
