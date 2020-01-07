using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Abc.Zerio.Alt.Buffers
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    internal readonly struct BufferSegmentId
    {
        private readonly long _value;

        public BufferSegmentId(byte poolId, int bufferId, int segmentId)
        {
            if ((uint)bufferId >= (1 << 24))
                throw new ArgumentOutOfRangeException(nameof(bufferId));

            var left = (long)((poolId << 24) | bufferId) << 32;
            var right = (long)segmentId;
            _value = left | right;
            Debug.Assert(_value >= 0);
        }

        public BufferSegmentId(long value)
        {
            _value = value;
        }

        public long Value => _value;
        public int PoolId => (int)(_value >> 32 + 24);
        public int BufferId => ((int)(_value >> 32)) & ((1 << 24) - 1);
        public int SegmentId => (int)(_value & ((1L << 32) - 1));
    }
}
