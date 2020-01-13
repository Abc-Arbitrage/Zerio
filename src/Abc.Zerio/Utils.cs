using System;
using System.Runtime.CompilerServices;

namespace Abc.Zerio
{
    internal static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindNextPositivePowerOfTwo(int value)
        {
            unchecked
            {
                return 1 << (32 - NumberOfLeadingZeros(value - 1));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int value, int alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Align(long value, long alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfLeadingZeros(int i)
        {
#if NETCOREAPP3_0
            if (System.Runtime.Intrinsics.X86.Lzcnt.IsSupported)
            {
                return (int)System.Runtime.Intrinsics.X86.Lzcnt.LeadingZeroCount((uint)i);
            }
#endif

            unchecked
            {
                // HD, Figure 5-6
                if (i == 0)
                {
                    return 32;
                }

                int n = 1;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && ((value & (~value + 1)) == value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAligned(long address, int alignment)
        {
            if (!IsPowerOfTwo(alignment))
            {
                throw new ArgumentException("Alignment must be a power of 2: alignment=" + alignment);
            }

            return (address & (alignment - 1)) == 0;
        }
    }
}
