using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal sealed unsafe class RequestEntryBuffer : IDisposable
    {
        private readonly IntPtr _buffer;
        private readonly IntPtr _bufferId;

        public RequestEntry* FirstEntry => (RequestEntry*)_buffer.ToPointer();
        public int EntryReservedSpaceSize { get; }

        public RequestEntryBuffer(int bufferCount, int bufferLength)
        {
            // Example with bufferLength = 1024
            //
            //  |- RequestEntry reserved space -------------------------------------------------|- RequestEntry reserved space  ------------------------------------------------|
            //  |                                                                               |                                                                               |
            //  |- RequestEntry #1 ----------------------------------------|                    |- RequestEntry #2-----------------------------------------|                    |
            //  |                                                          |                    |                                                          |                    |
            //  |           |- RIO_BUF ------------------------------------|- Data - ... -------|           |- RIO_BUF ------------------------------------|- Data - ... -------|
            //  |           |                                              |                    |           |                                              |                    |
            // [0]         [4]                    [12]        [16]        [20]                 [1044]      [1048]                 [1056]      [1060]      [1064]                |
            //  +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+
            //  | Fields    | BufferId             | Offset    | Length    | Buffer segment     | Fields    | BufferId             | Offset    | Length    | Buffer segment     |
            //  |           | IntPtr               | int       | int       | bytes              |           | IntPtr               | int       | int       | bytes              |
            //  | n bytes   | 8 bytes              | 4         | 4         | 1024               | n bytes   | 8 bytes              | 4         | 4         | 1024               |
            //  +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+
            //  | SessionId                        | 20        | 1024      |                                                       | 1064      | 1024      |
            //    Type                               +---------------------^                                                         +---------------------^
            //    BufferId

            var entryLength = sizeof(RequestEntry);
            EntryReservedSpaceSize = entryLength + bufferLength;

            var totalBufferLength = (uint)(bufferCount * EntryReservedSpaceSize);

            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            _buffer = Kernel32.VirtualAlloc(IntPtr.Zero, totalBufferLength, allocationType, Kernel32.Consts.PAGE_READWRITE);

            _bufferId = WinSock.Extensions.RegisterBuffer(_buffer, totalBufferLength);
            if (_bufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();

            var currentEntry = FirstEntry;

            for (var i = 0; i < bufferCount; i++)
            {
                currentEntry->RioBufferDescriptor.BufferId = _bufferId;
                currentEntry->RioBufferDescriptor.Offset = i * EntryReservedSpaceSize + sizeof(RequestEntry);
                currentEntry->RioBufferDescriptor.Length = bufferLength;
                currentEntry = (RequestEntry*)((byte*)(currentEntry + 1) + bufferLength);
            }
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                WinSock.Extensions.DeregisterBuffer(_bufferId);
            }
            finally
            {
                Kernel32.VirtualFree(_buffer, 0, Kernel32.Consts.MEM_RELEASE);
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RequestEntryBuffer()
        {
            ReleaseUnmanagedResources();
        }
    }
}
