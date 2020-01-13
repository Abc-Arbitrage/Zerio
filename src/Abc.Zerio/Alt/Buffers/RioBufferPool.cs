using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    internal class RioBufferPool : IDisposable
    {
        private readonly SharedSegmentPool _sharedSegmentPool;
        private const byte _sharedSendPoolId = 1;

        public RioBufferPool(BufferPoolOptions options = null, CancellationToken ct = default)
        {
            options ??= new BufferPoolOptions();
            Options = options;
            CancellationToken = ct;
            _sharedSegmentPool = new SharedSegmentPool(_sharedSendPoolId, options.SendSegmentSize, ct);

            if (Options.UseSharedSendPool)
            {
                // the buffer is owned by _sharedSendPool
                var _ = AllocateBuffer();
            }
        }

        public BufferPoolOptions Options { get; }

        public CancellationToken CancellationToken { get; }

        private RegisteredBuffer AllocateBuffer()
        {
            var buffer = new RegisteredBuffer(Options.SendSegmentCount * 2, Options.SendSegmentSize);
            buffer.PoolId = _sharedSendPoolId;
            _sharedSegmentPool.AddBuffer(buffer);
            Debug.Assert(buffer.IsPooled);
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRent(out RioSegment segment)
        {
            if (_sharedSegmentPool.TryRent(out segment))
            {
                Debug.Assert(segment.Id.PoolId == _sharedSendPoolId, "segment.Id.PoolId == _sharedSendPoolId");
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(RioSegment segment)
        {
            _sharedSegmentPool.Return(segment);
        }

        public RioSegment this[long correlationId]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var id = new BufferSegmentId(correlationId);
                return this[id];
            }
        }

        public RioSegment this[BufferSegmentId id]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sharedSegmentPool.GetBuffer(id.BufferId)[id.SegmentId];
        }

        public void Dispose()
        {
            _sharedSegmentPool.Dispose();
        }
    }
}
