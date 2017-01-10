using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Abc.Zerio.Interop
{
    public static class Kernel32
    {
        [DllImport("Kernel32", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr VirtualAlloc([In] IntPtr lpAddress, [In] uint dwSize, [In] int flAllocationType, [In] int flProtect);

        [DllImport("Kernel32", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern bool VirtualFree([In] IntPtr lpAddress, [In] uint dwSize, [In] int dwFreeType);

        public static class Consts
        {
            public const int MEM_COMMIT = 0x00001000;
            public const int MEM_RESERVE = 0x00002000;
            public const int MEM_RELEASE = 0x8000;
            public const int PAGE_READWRITE = 0x04;
        }
    }
}
