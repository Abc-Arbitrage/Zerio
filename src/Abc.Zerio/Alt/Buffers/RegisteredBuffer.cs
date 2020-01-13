using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using Abc.Zerio.Interop;
using static Abc.Zerio.Utils;

namespace Abc.Zerio.Alt.Buffers
{
    internal unsafe class RegisteredBuffer : MemoryManager<byte>, IDisposable
    {
        private static readonly ConcurrentDictionary<IntPtr, RegisteredBuffer> _buffers = new ConcurrentDictionary<IntPtr, RegisteredBuffer>();

        public static RegisteredBuffer GetBuffer(IntPtr bufferId)
        {
            return _buffers.TryGetValue(bufferId, out var value) ? value : null;
        }

        public byte* Start { get; }
        public int SegmentLength { get; }
        public int Count { get; }

        /// <summary>
        /// Non-zero when the buffer is pooled.
        /// </summary>
        internal volatile bool IsPooled;

        internal byte PoolId;
        internal int BufferId;

        /// <summary>
        /// Number of pooled buffer segment from this registered buffer.
        /// </summary>
        public int PooledSegmentCount;

        public IntPtr RegisteredBufferId { get; }

        public RegisteredBuffer(int segmentCount, int segmentLength)
        {
            IsPooled = false;
            PooledSegmentCount = 0;

            const int pageSize = 4096;
            const int minSegmentSize = 1024;

            segmentLength = Math.Max(segmentLength, minSegmentSize);

            if (!IsPowerOfTwo(segmentLength))
            {
                throw new ArgumentException("Segment length is now a power of 2");
            }

            uint totalBufferLength = checked((uint)(segmentCount * segmentLength));

            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            Start = (byte*)Kernel32.VirtualAlloc(IntPtr.Zero, totalBufferLength, allocationType, Kernel32.Consts.PAGE_READWRITE);
            Debug.Assert(IsAligned((long)Start, pageSize));
            Count = (int)(totalBufferLength / segmentLength);
            SegmentLength = segmentLength;

            RegisteredBufferId = WinSock.Extensions.RegisterBuffer((IntPtr)Start, totalBufferLength);
            if (RegisteredBufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();

            if (!_buffers.TryAdd(RegisteredBufferId, this))
                throw new InvalidOperationException();
        }

        public RioSegment this[int index]
        {
            get
            {
                if ((uint)index >= Count)
                    throw new IndexOutOfRangeException();

                var offset = index * SegmentLength;

                var rioBuf = new RIO_BUF() { BufferId = RegisteredBufferId, Offset = offset, Length = SegmentLength };
                var pointer = Start + offset;
                var id = new BufferSegmentId(PoolId, BufferId, index);
                return new RioSegment(this, pointer, rioBuf, id);
            }
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                WinSock.Extensions.DeregisterBuffer(RegisteredBufferId);
                // Static _buffers keeps a reference to the buffer instance, session could be disposed without disposing it's RB
                _buffers.TryRemove(RegisteredBufferId, out _);
            }
            finally
            {
                Kernel32.VirtualFree((IntPtr)Start, 0, Kernel32.Consts.MEM_RELEASE);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            
            GC.SuppressFinalize(this);
        }

        ~RegisteredBuffer()
        {
            ReleaseUnmanagedResources();
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
        }

        public override Span<byte> GetSpan()
        {
            return new Span<byte>(Start, Count * SegmentLength);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            return new MemoryHandle(Start);
        }

        public override void Unpin()
        {
            // noop;
        }
    }

    //public static class RioBufExtensions
    //{
    //    internal static RegisteredBuffer GetBuffer(this RIO_BUF buf)
    //    {
    //        return RegisteredBuffer.GetBuffer(buf.BufferId);
    //    }
    //}
}
