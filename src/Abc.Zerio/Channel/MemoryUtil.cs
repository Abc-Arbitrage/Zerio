using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Abc.Zerio.Channel
{
    internal static class MemoryUtil
    {
        public const int CacheLineLength = 64;

        public static bool IsAlignedToCacheLine(long offset) => offset % CacheLineLength == 0;

        [SuppressUnmanagedCodeSecurity]
        [DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        public static extern void ZeroMemory(IntPtr dest, IntPtr size);
    }
}
