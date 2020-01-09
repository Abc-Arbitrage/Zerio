using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    public class BufferPoolOptions
    {
        public int SendSegmentCount { get; }
        public int SendSegmentSize { get; }
        public int ReceiveSegmentCount { get; }
        public int ReceiveSegmentSize { get; }
        public bool UseSharedSendPool { get; }

        public BufferPoolOptions(int sendSegmentCount = 2048, int sendSegmentSize = 2048, int receiveSegmentCount = 128, int receiveSegmentSize = 16384, bool useSharedSendPool = true)
        {
            SendSegmentCount = sendSegmentCount;
            SendSegmentSize = sendSegmentSize;
            ReceiveSegmentCount = receiveSegmentCount;
            ReceiveSegmentSize = receiveSegmentSize;
            UseSharedSendPool = useSharedSendPool;
        }
    }

    internal class RioBufferPool : IDisposable
    {
        private readonly ISegmentPool _sharedSendPool;
        private const byte _sharedSendPoolId = 1;

        public RioBufferPool(BufferPoolOptions options = null, CancellationToken ct = default)
        {
            options = options ?? new BufferPoolOptions();
            Options = options;
            CancellationToken = ct;
            _sharedSendPool = new DefaultSegmentPool(_sharedSendPoolId, options.SendSegmentSize, ct);

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
            var buffer = new RegisteredBuffer(Options.SendSegmentCount, Options.SendSegmentSize);
            _sharedSendPool.AddBuffer(buffer);
            Debug.Assert(buffer.IsPooled);
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRent(out RioSegment segment)
        {
            return _sharedSendPool.TryRent(out segment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(RioSegment segment)
        {
            _sharedSendPool.Return(segment);
        }

        public void Return(long correlationId)
        {
            var id = new BufferSegmentId(correlationId);
            if (id.PoolId != _sharedSendPoolId)
                throw new InvalidOperationException();
            var segment = _sharedSendPool.GetBuffer(id.BufferId)[id.SegmentId];
            _sharedSendPool.Return(segment);
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
            get => _sharedSendPool.GetBuffer(id.BufferId)[id.SegmentId];
        }

        public void Dispose()
        {
            _sharedSendPool.Dispose();
        }
    }
}
