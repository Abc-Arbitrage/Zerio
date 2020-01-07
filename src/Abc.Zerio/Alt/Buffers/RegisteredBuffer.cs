using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using Abc.Zerio.Interop;
using static Abc.Zerio.Utils;

namespace Abc.Zerio.Alt.Buffers
{
    internal unsafe class RegisteredBuffer : CriticalFinalizerObject, IDisposable
    {
        private static readonly ConcurrentDictionary<IntPtr, RegisteredBuffer> _buffers = new ConcurrentDictionary<IntPtr, RegisteredBuffer>();

        public static RegisteredBuffer GetBuffer(IntPtr bufferId)
        {
            return _buffers.TryGetValue(bufferId, out var value) ? value : null;
        }

        private readonly byte* _start;
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
            _start = (byte*)Kernel32.VirtualAlloc(IntPtr.Zero, totalBufferLength, allocationType, Kernel32.Consts.PAGE_READWRITE);
            Debug.Assert(IsAligned((long)_start, pageSize));
            Count = (int)(totalBufferLength / segmentLength);
            SegmentLength = segmentLength;

            RegisteredBufferId = WinSock.Extensions.RegisterBuffer((IntPtr)_start, totalBufferLength);
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
                var pointer = _start + offset;
                var id = new BufferSegmentId(PoolId, BufferId, index);
                return new RioSegment(pointer, rioBuf, id);
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
                Kernel32.VirtualFree((IntPtr)_start, 0, Kernel32.Consts.MEM_RELEASE);
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RegisteredBuffer()
        {
            ReleaseUnmanagedResources();
        }
    }

    public static class RioBufExtensions
    {
        internal static RegisteredBuffer GetBuffer(this RIO_BUF buf)
        {
            return RegisteredBuffer.GetBuffer(buf.BufferId);
        }
    }
}
