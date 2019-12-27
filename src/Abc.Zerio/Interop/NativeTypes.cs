using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Abc.Zerio.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RIO_EXTENSION_FUNCTION_TABLE
    {
        internal uint cbSize;

        internal IntPtr RIOReceive;
        internal IntPtr RIOReceiveEx;
        internal IntPtr RIOSend;
        internal IntPtr RIOSendEx;
        internal IntPtr RIOCloseCompletionQueue;
        internal IntPtr RIOCreateCompletionQueue;
        internal IntPtr RIOCreateRequestQueue;
        internal IntPtr RIODequeueCompletion;
        internal IntPtr RIODeregisterBuffer;
        internal IntPtr RIONotify;
        internal IntPtr RIORegisterBuffer;
        internal IntPtr RIOResizeCompletionQueue;
        internal IntPtr RIOResizeRequestQueue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RIO_BUF
    {
        public IntPtr BufferId;
        public int Offset;
        public int Length;

        public const int Size =  8 + sizeof(int) + sizeof(int);
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RIO_NOTIFICATION_COMPLETION
    {
        [FieldOffset(0)]
        public RioNotificationCompletionType Type;

        [FieldOffset(4)]
        public RioNotificationCompletionIocp Iocp;

        [FieldOffset(4)]
        public RioNotificationCompletionEvent Event;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RIO_RESULT
    {
        internal int Status;
        internal uint BytesTransferred;
        internal long ConnectionCorrelation;
        internal long RequestCorrelation;
    }

    [Flags]
    internal enum RIO_SEND_FLAGS : uint
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        COMMIT_ONLY = 0x00000008
    }

    [Flags]
    internal enum RIO_RECEIVE_FLAGS : uint
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        WAITALL = 0x00000004,
        COMMIT_ONLY = 0x00000008
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RioNativeOverlapped
    {
        public IntPtr EventHandle;
        public IntPtr InternalHigh;
        public IntPtr InternalLow;
        public int OffsetHigh;
        public int OffsetLow;
        public int SocketIndex;
        public byte Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RioNotificationCompletionEvent
    {
        public IntPtr IocpHandle;
        public int NotifyReset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct RioNotificationCompletionIocp
    {
        public IntPtr IocpHandle;
        public ulong QueueCorrelation;
        public NativeOverlapped* Overlapped;
    }

    internal enum RioNotificationCompletionType
    {
        EventCompletion = 1,
        IocpCompletion = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SockaddrIn
    {
        public AddressFamilies sin_family;
        public ushort sin_port;
        public InAddr sin_addr;
        public fixed byte sin_zero [8];

        public const ulong INADDR_ANY = 0x00000000;
    }

    [Flags]
    internal enum SocketFlags : uint
    {
        WSA_FLAG_OVERLAPPED = 0x01,
        WSA_FLAG_MULTIPOINT_C_ROOT = 0x02,
        WSA_FLAG_MULTIPOINT_C_LEAF = 0x04,
        WSA_FLAG_MULTIPOINT_D_ROOT = 0x08,
        WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
        WSA_FLAG_ACCESS_SYSTEM_SECURITY = 0x40,
        WSA_FLAG_NO_HANDLE_INHERIT = 0x80,
        WSA_FLAG_REGISTERED_IO = 0x100,
    }

    public enum SocketType : short
    {
        SOCK_STREAM = 1,
        SOCK_DGRAM = 2,
        SOCK_RAW = 3,
        SOCK_RDM = 4,
        SOCK_SEQPACKET = 5,
    }

    internal struct Version
    {
        internal readonly ushort Raw;

        internal Version(byte major, byte minor)
        {
            Raw = major;
            Raw <<= 8;
            Raw += minor;
        }

        internal byte Major
        {
            get
            {
                var result = Raw;
                result >>= 8;
                return (byte)result;
            }
        }

        internal byte Minor
        {
            get
            {
                var result = Raw;
                result &= 0x00FF;
                return (byte)result;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WSAData
    {
        internal short wVersion;
        internal short wHighVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string szDescription;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string szSystemStatus;

        internal short iMaxSockets;
        internal short iMaxUdpDg;
        internal IntPtr lpVendorInfo;
    }

    public enum AddressFamilies : short
    {
        AF_UNSPEC = 0, // unspecified
        AF_UNIX = 1, // local to host (pipes, portals)
        AF_INET = 2, // internetwork: UDP, TCP, etc.
        AF_IMPLINK = 3, // arpanet imp addresses
        AF_PUP = 4, // pup protocols: e.g. BSP
        AF_CHAOS = 5, // mit CHAOS protocols
        AF_NS = 6, // XEROX NS protocols
        AF_IPX = AF_NS, // IPX protocols: IPX, SPX, etc.
        AF_ISO = 7, // ISO protocols
        AF_OSI = AF_ISO, // OSI is ISO
        AF_ECMA = 8, // european computer manufacturers
        AF_DATAKIT = 9, // datakit protocols
        AF_CCITT = 10, // CCITT protocols, X.25 etc
        AF_SNA = 11, // IBM SNA
        AF_DECnet = 12, // DECnet
        AF_DLI = 13, // Direct data link interface
        AF_LAT = 14, // LAT
        AF_HYLINK = 15, // NSC Hyperchannel
        AF_APPLETALK = 16, // AppleTalk
        AF_NETBIOS = 17, // NetBios-style addresses
        AF_VOICEVIEW = 18, // VoiceView
        AF_FIREFOX = 19, // Protocols from Firefox
        AF_UNKNOWN1 = 20, // Somebody is using this!
        AF_BAN = 21, // Banyan
        AF_ATM = 22, // Native ATM Services
        AF_INET6 = 23, // Internetwork Version 6
        AF_CLUSTER = 24, // Microsoft Wolfpack
        AF_12844 = 25, // IEEE 1284.4 WG AF
        AF_IRDA = 26, // IrDA
        AF_NETDES = 28, // Network Designers OSI & gateway
        AF_TCNPROCESS = 29,
        AF_TCNMESSAGE = 30,
        AF_ICLFXBM = 31,
        AF_BTH = 32, // Bluetooth RFCOMM/L2CAP protocols
        AF_LINK = 33,
        AF_HYPERV = 34,
        AF_MAX = 35,
    }

    public enum Protocol
    {
        IPPROTO_HOPOPTS = 0, // IPv6 Hop-by-Hop options
        IPPROTO_ICMP = 1,
        IPPROTO_IGMP = 2,
        IPPROTO_GGP = 3,
        IPPROTO_IPV4 = 4,
        IPPROTO_ST = 5,
        IPPROTO_TCP = 6,
        IPPROTO_CBT = 7,
        IPPROTO_EGP = 8,
        IPPROTO_IGP = 9,
        IPPROTO_PUP = 12,
        IPPROTO_UDP = 17,
        IPPROTO_IDP = 22,
        IPPROTO_RDP = 27,
        IPPROTO_IPV6 = 41, // IPv6 header
        IPPROTO_ROUTING = 43, // IPv6 Routing header
        IPPROTO_FRAGMENT = 44, // IPv6 fragmentation header
        IPPROTO_ESP = 50, // encapsulating security payload
        IPPROTO_AH = 51, // authentication header
        IPPROTO_ICMPV6 = 58, // ICMPv6
        IPPROTO_NONE = 59, // IPv6 no next header
        IPPROTO_DSTOPTS = 60, // IPv6 Destination options
        IPPROTO_ND = 77,
        IPPROTO_ICLFXBM = 78,
        IPPROTO_PIM = 103,
        IPPROTO_PGM = 113,
        IPPROTO_L2TP = 115,
        IPPROTO_SCTP = 132,
        IPPROTO_RAW = 255,
        IPPROTO_MAX = 256,
        //
        //  These are reserved for internal use by Windows.
        //
        IPPROTO_RESERVED_RAW = 257,
        IPPROTO_RESERVED_IPSEC = 258,
        IPPROTO_RESERVED_IPSECOFFLOAD = 259,
        IPPROTO_RESERVED_WNV = 260,
        IPPROTO_RESERVED_MAX = 261
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    internal struct InAddr
    {
        [FieldOffset(0)]
        public byte B1;

        [FieldOffset(1)]
        public byte B2;

        [FieldOffset(2)]
        public byte B3;

        [FieldOffset(3)]
        public byte B4;

        public InAddr(byte[] addressbytes)
        {
            B1 = addressbytes[0];
            B2 = addressbytes[1];
            B3 = addressbytes[2];
            B4 = addressbytes[3];
        }
    }
}
