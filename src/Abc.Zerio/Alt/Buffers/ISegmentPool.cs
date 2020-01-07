using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Alt.Buffers
{
    // TODO add proper (fast) pool implementation, drop interface

    internal interface ISegmentPool
    {
        RioSegment Rent();
        void Return(RioSegment segment);
        void AddBuffer(RegisteredBuffer segment);
        RegisteredBuffer GetBuffer(int bufferIdOneBased);
        int SegmentLength { get; }
    }

    internal class DefaultSegmentPool : ISegmentPool
    {
        private readonly List<RegisteredBuffer> _buffers = new List<RegisteredBuffer>(64);

        private readonly byte _poolId;

        private readonly CancellationToken _ct;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, int.MaxValue);
        private ConcurrentBag<RioSegment> _cb;

        public DefaultSegmentPool(byte poolId, int segmentLength, CancellationToken ct = default)
        {
            _poolId = poolId;
            SegmentLength = segmentLength;
            _ct = ct;
            _cb = new ConcurrentBag<RioSegment>();
        }

        public int SegmentLength { get; }

        public RioSegment Rent()
        {
            _semaphore.Wait(_ct);
            if (_cb.TryTake(out var segment))
            {
                return segment;
            }

            throw new InvalidOperationException("The semaphore must guarantee that we have an available segment.");
        }

        public void Return(RioSegment segment)
        {
            var buffer = segment.RioBuf.GetBuffer();
            if (buffer.IsPooled)
            {
                _cb.Add(segment);
                _semaphore.Release();
            }
            else
            {
                var remaining = Interlocked.Decrement(ref buffer.PooledSegmentCount);
                if (remaining == 0)
                {
                    buffer.Dispose();
                }
            }
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
                    _buffers.Add(buffer);
                    idx = _buffers.Count;
                }
            }

            buffer.PoolId = _poolId;
            buffer.BufferId = idx;
            buffer.IsPooled = true;

            for (int i = 0; i < buffer.Count; i++)
            {
                _cb.Add(buffer[i]);
            }

            _semaphore.Release(buffer.Count);
        }

        // ReSharper disable once InconsistentlySynchronizedField
        public RegisteredBuffer GetBuffer(int bufferIdOneBased) => _buffers[bufferIdOneBased - 1];
    }
}
