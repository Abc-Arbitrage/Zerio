using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Alt.Buffers
{
    internal class RioBufferPools : IDisposable
    {
        private ISegmentPool _poolSend;
        private ISegmentPool _poolReceive;
        private RegisteredBuffer _sharedSmall;
        private RegisteredBuffer _sharedLarge;
        private ISegmentPool[] _pools = new ISegmentPool[2];

        public const byte SendPoolId = 0;
        public const byte ReceivePoolId = 1;

        public RioBufferPools(int sendSegmentCount = 2048, int sendSegmentSize = 1024, int receiveSegmentCount = 128, int receiveSegmentSize = 16384, CancellationToken ct = default)
        {
            SendSegmentCount = sendSegmentCount;
            SendSegmentSize = sendSegmentSize;
            ReceiveSegmentCount = receiveSegmentCount;
            ReceiveSegmentSize = receiveSegmentSize;
            CancellationToken = ct;

            _poolSend = new DefaultSegmentPool(SendPoolId, SendSegmentSize, ct);
            _poolReceive = new DefaultSegmentPool(ReceivePoolId, ReceiveSegmentSize, ct);
            _pools[SendPoolId] = _poolSend;
            _pools[ReceivePoolId] = _poolReceive;

            // create shared buffers
            // session queues are 2x size of segment counts to accomodate segments from shared buffers during bursts
            _sharedSmall = new RegisteredBuffer(SendSegmentCount, SendSegmentSize);
            _sharedLarge = new RegisteredBuffer(ReceiveSegmentCount, ReceiveSegmentSize);

            _poolSend.AddBuffer(_sharedSmall);
            Debug.Assert(_sharedSmall.IsPooled);

            _poolReceive.AddBuffer(_sharedLarge);
            Debug.Assert(_sharedLarge.IsPooled);
        }

        public CancellationToken CancellationToken { get; }

        public int SendSegmentCount { get; }
        public int SendSegmentSize { get; }
        public int ReceiveSegmentCount { get; }
        public int ReceiveSegmentSize { get; }

        public RegisteredBuffer AllocateSendBuffer()
        {
            var buffer = new RegisteredBuffer(SendSegmentCount, SendSegmentSize);
            _poolSend.AddBuffer(buffer);
            Debug.Assert(buffer.IsPooled);
            return buffer;
        }

        public RegisteredBuffer AllocateReceiveBuffer()
        {
            var buffer = new RegisteredBuffer(SendSegmentCount, SendSegmentSize);
            _poolSend.AddBuffer(buffer);
            Debug.Assert(buffer.IsPooled);
            return buffer;
        }

        public RioSegment RentSendSegment()
        {
            return _poolSend.Rent();
        }

        public void ReturnSendSegment(RioSegment segment)
        {
            Debug.Assert(segment.Length <= SendSegmentSize);
            segment.RioBuf.Length = SendSegmentSize;
            _poolSend.Return(segment);
        }

        public RioSegment RentReceiveSegment()
        {
            return _poolReceive.Rent();
        }

        public void ReturnReceiveSegment(RioSegment segment)
        {
            Debug.Assert(segment.Length <= ReceiveSegmentSize);
            segment.RioBuf.Length = ReceiveSegmentSize;
            _poolReceive.Return(segment);
        }

        public void ReturnSegment(RioSegment segment)
        {
            var pool = _pools[segment.Id.PoolId];
            Debug.Assert(segment.Length <= pool.SegmentLength);
            segment.RioBuf.Length = pool.SegmentLength;
            pool.Return(segment);
        }

        public void ReturnSegment(long correlationId)
        {
            var id = new BufferSegmentId(correlationId);
            if(id.PoolId != SendPoolId)
                throw new InvalidOperationException();
            var pool = _pools[id.PoolId];
            var segment = pool.GetBuffer(id.BufferId)[id.SegmentId];
            segment.RioBuf.Length = pool.SegmentLength;
            pool.Return(segment);
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
            get
            {
                return _pools[id.PoolId].GetBuffer(id.BufferId)[id.SegmentId];
            }
        }

        public void Dispose()
        {
            _sharedSmall.IsPooled = false;
            _sharedLarge.IsPooled = false;

            // TODO we could acquire all buffers from both pool
            // that will block, when we acquire them here we should
            // set all RBs as not pooled and return all segments
            // Need to maintain global segment counter for that
            // But need something much simpler
        }
    }
}
