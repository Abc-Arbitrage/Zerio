using System.Runtime.CompilerServices;

namespace Abc.Zerio.Channel
{
    public class BitUtil
    {
        public const int CacheLineLength = 64;
     
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindNextPositivePowerOfTwo(int value) => 1 << (32 - NumberOfLeadingZeros(value - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int value, int alignment) => (value + (alignment - 1)) & ~(alignment - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfLeadingZeros(int i)
        {
            if (i == 0)
                return 32;

            var n = 1;
            if ((int)((uint)i >> 16) == 0)
            {
                n += 16;
                i <<= 16;
            }

            if ((int)((uint)i >> 24) == 0)
            {
                n += 8;
                i <<= 8;
            }

            if ((int)((uint)i >> 28) == 0)
            {
                n += 4;
                i <<= 4;
            }

            if ((int)((uint)i >> 30) == 0)
            {
                n += 2;
                i <<= 2;
            }

            n -= (int)((uint)i >> 31);
            return n;
        }
    }
}
