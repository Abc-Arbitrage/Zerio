using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;

namespace Abc.Zerio.Interop
{
    [SuppressUnmanagedCodeSecurity]
    public unsafe class WinSock
    {
        [DllImport("WS2_32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = true, ThrowOnUnmappableChar = true)]
        internal static extern SocketError WSAStartup([In] short wVersionRequested, [Out] out WSAData lpWSAData);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int WSACleanup();

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int WSAIoctl([In] IntPtr socket, [In] uint dwIoControlCode, [In] ref Guid lpvInBuffer, [In] uint cbInBuffer, [In, Out] ref RIO_EXTENSION_FUNCTION_TABLE lpvOutBuffer, [In] int cbOutBuffer, [Out] out uint lpcbBytesReturned, [In] IntPtr lpOverlapped, [In] IntPtr lpCompletionRoutine);

        [DllImport("WS2_32.dll", SetLastError = true, EntryPoint = "WSAIoctl")]
        internal static extern int WSAIoctl2([In] IntPtr socket, [In] uint dwIoControlCode, [In] ref Guid lpvInBuffer, [In] uint cbInBuffer, [In, Out] ref IntPtr lpvOutBuffer, [In] int cbOutBuffer, [Out] out uint lpcbBytesReturned, [In] IntPtr lpOverlapped, [In] IntPtr lpCompletionRoutine);

        [DllImport("WS2_32.dll", SetLastError = true, EntryPoint = "WSAIoctl")]
        internal static extern int WSAIoctlGeneral([In] IntPtr socket, [In] uint dwIoControlCode, [In] int* lpvInBuffer, [In] uint cbInBuffer, [In] int* lpvOutBuffer, [In] int cbOutBuffer, [Out] out uint lpcbBytesReturned, [In] IntPtr lpOverlapped, [In] IntPtr lpCompletionRoutine);

        [DllImport("ws2_32.dll", EntryPoint = "WSAIoctl", SetLastError = true)]
        internal static extern SocketError WSAIoctl_Blocking([In] IntPtr socketHandle, [In] int ioControlCode, [In] byte[] inBuffer, [In] int inBufferSize, [Out] byte[] outBuffer, [In] int outBufferSize, out int bytesTransferred, [In] IntPtr overlapped, [In] IntPtr completionRoutine);

        [DllImport("WS2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr WSASocket([In] AddressFamilies af, [In] SocketType type, [In] Protocol protocol, [In] IntPtr lpProtocolInfo, [In] int group, [In] SocketFlags dwFlags);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern bool WSAGetOverlappedResult(IntPtr socket, [In] RioNativeOverlapped* lpOverlapped, out int lpcbTransfer, bool fWait, out int lpdwFlags);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern IntPtr accept(IntPtr s, IntPtr addr, IntPtr addrlen);

        [DllImport("WS2_32.dll", SetLastError = false)]
        internal static extern int connect([In] IntPtr s, [In] ref SockaddrIn name, [In] int namelen);

        [DllImport("WS2_32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern int bind(IntPtr s, ref SockaddrIn name, int namelen);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int listen(IntPtr s, int backlog);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int closesocket(IntPtr s);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int setsockopt(IntPtr s, int level, int optname, char* optval, int optlen);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern int getsockopt(IntPtr s, int level, int optname, char* optval, int* optlen);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern ushort htons([In] ushort hostshort);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern ushort ntohs([In] ushort netshort);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern ulong htonl([In] ulong hostshort);

        [DllImport("WS2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockname([In] IntPtr s, [Out] byte* socketAddress, [In, Out] ref int socketAddressSize);

        [DllImport("WS2_32.dll")]
        internal static extern int WSAGetLastError();

        public static class Extensions
        {
            internal static RIORegisterBufferFunc RegisterBuffer;
            internal static RIOCreateCompletionQueueFunc CreateCompletionQueue;
            internal static RIOCreateRequestQueueFunc CreateRequestQueue;
            internal static RIOReceiveFunc Receive;
            internal static RIOSendFunc Send;
            internal static RIONotifyFunc Notify;
            internal static RIOCloseCompletionQueueAction CloseCompletionQueue;
            internal static RIODequeueCompletionFunc DequeueCompletion;
            internal static RIODeregisterBufferAction DeregisterBuffer;
            internal static RIOResizeCompletionQueueFunc ResizeCompletionQueue;
            internal static RIOResizeRequestQueueFunc ResizeRequestQueue;
            internal static AcceptExFunc AcceptEx;
            internal static ConnectExFunc ConnectEx;
            internal static DisconnectExFunc DisconnectEx;

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate IntPtr RIORegisterBufferFunc([In] IntPtr DataBuffer, [In] uint DataLength);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate void RIODeregisterBufferAction([In] IntPtr BufferId);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate bool RIOSendFunc([In] IntPtr SocketQueue, RIO_BUF* RioBuffer, [In] uint DataBufferCount, [In] RIO_SEND_FLAGS Flags, [In] long RequestCorrelation);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate bool RIOReceiveFunc([In] IntPtr SocketQueue, RIO_BUF* RioBuffer, [In] uint DataBufferCount, [In] RIO_RECEIVE_FLAGS Flags, [In] long RequestCorrelation);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate CompletionQueueHandle RIOCreateCompletionQueueFunc([In] uint QueueSize, [In, Optional] RIO_NOTIFICATION_COMPLETION* NotificationCompletion);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate void RIOCloseCompletionQueueAction([In] IntPtr CQ);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate IntPtr RIOCreateRequestQueueFunc([In] IntPtr Socket, [In] uint MaxOutstandingReceive, [In] uint MaxReceiveDataBuffers, [In] uint MaxOutstandingSend, [In] uint MaxSendDataBuffers, [In] CompletionQueueHandle ReceiveCQ, [In] CompletionQueueHandle SendCQ, [In] int ConnectionCorrelation);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate uint RIODequeueCompletionFunc([In] IntPtr CQ, [In] RIO_RESULT* ResultArray, [In] uint ResultArrayLength);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate int RIONotifyFunc([In] IntPtr CQ);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate bool RIOResizeCompletionQueueFunc([In] IntPtr CQ, [In] uint QueueSize);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true), SuppressUnmanagedCodeSecurity]
            internal delegate bool RIOResizeRequestQueueFunc([In] IntPtr RQ, [In] uint MaxOutstandingReceive, [In] uint MaxOutstandingSend);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate bool AcceptExFunc([In] IntPtr sListenSocket, [In] IntPtr sAcceptSocket, [In] IntPtr lpOutputBuffer, [In] int dwReceiveDataLength, [In] int dwLocalAddressLength, [In] int dwRemoteAddressLength, [Out] out int lpdwBytesReceived, [In] RioNativeOverlapped* lpOverlapped);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate bool ConnectExFunc([In] IntPtr s, [In] SockaddrIn name, [In] int namelen, [In] IntPtr lpSendBuffer, [In] uint dwSendDataLength, [Out] out uint lpdwBytesSent, [In] RioNativeOverlapped* lpOverlapped);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false), SuppressUnmanagedCodeSecurity]
            internal delegate bool DisconnectExFunc([In] IntPtr hSocket, [In] RioNativeOverlapped* lpOverlapped, [In] uint dwFlags, [In] uint reserved);
        }

        private static readonly object _lock = new object();
        private static bool _initialized;
        private static Exception _initializationException;

        public static void EnsureIsInitialized()
        {
            if (!Environment.Is64BitProcess)
                throw new InvalidOperationException("32 bit processes are not supported");

            lock (_lock)
            {
                if (!_initialized)
                {
                    Initialize();
                    _initialized = true;
                }
            }

            if (_initializationException != null)
                throw new InvalidOperationException("Unable to initialize WinSock", _initializationException);
        }

        private static void Initialize()
        {
            var version = new Version(2, 2);
            WSAData data;
            var result = WSAStartup((short)version.Raw, out data);
            if (result != 0 && CaptureInitializationWsaError())
                return;

            var tempSocket = WSASocket(AddressFamilies.AF_INET, SocketType.SOCK_STREAM, Protocol.IPPROTO_TCP, IntPtr.Zero, 0, SocketFlags.WSA_FLAG_REGISTERED_IO | SocketFlags.WSA_FLAG_OVERLAPPED);
            if (CaptureInitializationWsaError())
                return;

            uint dwBytes;
            var acceptExId = new Guid("B5367DF1-CBAC-11CF-95CA-00805F48A192");
            var acceptExptr = IntPtr.Zero;

            var acceptExIoctlResult = WSAIoctl2(tempSocket, Consts.SIO_GET_EXTENSION_FUNCTION_POINTER, ref acceptExId, 16, ref acceptExptr, IntPtr.Size, out dwBytes, IntPtr.Zero, IntPtr.Zero);
            if (acceptExIoctlResult != 0 && CaptureInitializationWsaError())
                return;

            Extensions.AcceptEx = Marshal.GetDelegateForFunctionPointer<Extensions.AcceptExFunc>(acceptExptr);

            var connectExId = new Guid("25A207B9-DDF3-4660-8EE9-76E58C74063E");
            var connectExptr = IntPtr.Zero;

            var connectExIoctlResult = WSAIoctl2(tempSocket, Consts.SIO_GET_EXTENSION_FUNCTION_POINTER, ref connectExId, 16, ref connectExptr, IntPtr.Size, out dwBytes, IntPtr.Zero, IntPtr.Zero);
            if (connectExIoctlResult != 0 && CaptureInitializationWsaError())
                return;

            Extensions.ConnectEx = Marshal.GetDelegateForFunctionPointer<Extensions.ConnectExFunc>(connectExptr);

            var disconnectExId = new Guid("7FDA2E11-8630-436F-A031-F536A6EEC157");
            var disconnectExptr = IntPtr.Zero;

            var disconnectIoctlResult = WSAIoctl2(tempSocket, Consts.SIO_GET_EXTENSION_FUNCTION_POINTER, ref disconnectExId, 16, ref disconnectExptr, IntPtr.Size, out dwBytes, IntPtr.Zero, IntPtr.Zero);
            if (disconnectIoctlResult != 0 && CaptureInitializationWsaError())
                return;

            Extensions.DisconnectEx = Marshal.GetDelegateForFunctionPointer<Extensions.DisconnectExFunc>(disconnectExptr);

            var rio = new RIO_EXTENSION_FUNCTION_TABLE();
            var rioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");

            var rioIoctlResult = WSAIoctl(tempSocket, Consts.SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER, ref rioFunctionsTableId, 16, ref rio, sizeof(RIO_EXTENSION_FUNCTION_TABLE), out dwBytes, IntPtr.Zero, IntPtr.Zero);
            if (rioIoctlResult != 0 && CaptureInitializationWsaError())
                return;

            Extensions.RegisterBuffer = Marshal.GetDelegateForFunctionPointer<Extensions.RIORegisterBufferFunc>(rio.RIORegisterBuffer);
            Extensions.CreateCompletionQueue = Marshal.GetDelegateForFunctionPointer<Extensions.RIOCreateCompletionQueueFunc>(rio.RIOCreateCompletionQueue);
            Extensions.CreateRequestQueue = Marshal.GetDelegateForFunctionPointer<Extensions.RIOCreateRequestQueueFunc>(rio.RIOCreateRequestQueue);
            Extensions.Notify = Marshal.GetDelegateForFunctionPointer<Extensions.RIONotifyFunc>(rio.RIONotify);
            Extensions.DequeueCompletion = Marshal.GetDelegateForFunctionPointer<Extensions.RIODequeueCompletionFunc>(rio.RIODequeueCompletion);
            Extensions.Receive = Marshal.GetDelegateForFunctionPointer<Extensions.RIOReceiveFunc>(rio.RIOReceive);
            Extensions.Send = Marshal.GetDelegateForFunctionPointer<Extensions.RIOSendFunc>(rio.RIOSend);
            Extensions.CloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<Extensions.RIOCloseCompletionQueueAction>(rio.RIOCloseCompletionQueue);
            Extensions.DeregisterBuffer = Marshal.GetDelegateForFunctionPointer<Extensions.RIODeregisterBufferAction>(rio.RIODeregisterBuffer);
            Extensions.ResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<Extensions.RIOResizeCompletionQueueFunc>(rio.RIOResizeCompletionQueue);
            Extensions.ResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<Extensions.RIOResizeRequestQueueFunc>(rio.RIOResizeRequestQueue);

            closesocket(tempSocket);

            CaptureInitializationWsaError();
        }

        public static class Consts
        {
            public const int SOCKET_ERROR = -1;
            public const int INVALID_SOCKET = -1;
            public const int WSAEINTR = 10004;
            public const int WSAENOTSOCK = 10038;
            public const uint IOC_OUT = 0x40000000;
            public const uint IOC_IN = 0x80000000;
            public const uint IOC_INOUT = IOC_IN | IOC_OUT;
            public const uint IOC_WS2 = 0x08000000;
            public const uint SIO_GET_EXTENSION_FUNCTION_POINTER = IOC_INOUT | IOC_WS2 | 6;
            public const uint SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER = IOC_INOUT | IOC_WS2 | 36;
            public const uint SIO_LOOPBACK_FAST_PATH = IOC_IN | IOC_WS2 | 16;
            public const int TCP_NODELAY = 0x0001;
            public const int SO_REUSEADDR = 0x0004;
            public const int IPPROTO_TCP = 6;
            public const int SOL_SOCKET = 0xffff;
            public const int SOMAXCONN = 0x7fffffff;
            public const uint RIO_CORRUPT_CQ = 0xFFFFFFFF;
            public static readonly IntPtr RIO_INVALID_BUFFERID = new IntPtr(-1);
        }

        internal static void ThrowLastWsaError()
        {
            var error = WSAGetLastError();

            if (error != 0 && error != 997)
                throw new Win32Exception(error);
        }

        private static bool CaptureInitializationWsaError()
        {
            var error = WSAGetLastError();
            if (error != 0 && error != 997)
            {
                _initializationException = new Win32Exception(error);
                return true;
            }
            return false;
        }
    }
}
