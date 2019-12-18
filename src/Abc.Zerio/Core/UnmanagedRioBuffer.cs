using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal sealed unsafe class UnmanagedRioBuffer<T> : IDisposable
        where T : unmanaged, IRioBufferSegmentDescriptorContainer
    {
        private readonly IntPtr _buffer;
        private readonly IntPtr _bufferId;

        public T* FirstEntry => (T*)_buffer.ToPointer();
        public int EntryReservedSpaceSize { get; }
        public int Length { get; }

        public T* this[in int index] => FirstEntry + index * EntryReservedSpaceSize;

        public UnmanagedRioBuffer(int bufferCount, int bufferLength)
        {
            // Example with:
            //  - bufferLength = 1024
            //  - Entry of type T having a single int field
            //
            //  |- Entry reserved space --------------------------------------------------------|- Entry reserved space  -------------------------------------------------------|
            //  |                                                                               |                                                                               |
            //  |- Entry #1 -----------------------------------------------|                    |- Entry #2 -----------------------------------------------|                    |
            //  |                                                          |                    |                                                          |                    |
            //  |           |- RIO_BUF ------------------------------------|- Data - ... -------|           |- RIO_BUF ------------------------------------|- Data - ... -------|
            //  |           |                                              |                    |           |                                              |                    |
            // [0]         [4]                    [12]        [16]        [20]                 [1044]      [1048]                 [1056]      [1060]      [1064]                |
            //  +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+
            //  | IntField  | BufferId             | Offset    | Length    | Buffer segment     | IntField  | BufferId             | Offset    | Length    | Buffer segment     |
            //  |           | IntPtr               | int       | int       | bytes              |           | IntPtr               | int       | int       | bytes              |
            //  | 4 bytes   | 8 bytes              | 4         | 4         | 1024               | 4 bytes   | 8 bytes              | 4         | 4         | 1024               |
            //  +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+
            //  |                                  | 20        | 1024      |                                                       | 1064      | 1024      |
            //                                       +---------------------^                                                         +---------------------^
            

            Length = bufferCount;
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
                var bufferDescriptor = currentEntry->GetRioBufferDescriptor();
                
                bufferDescriptor->BufferId = _bufferId;
                bufferDescriptor->Offset = i * EntryReservedSpaceSize + sizeof(T);
                bufferDescriptor->Length = bufferLength;
                
                currentEntry = (T*)((byte*)(currentEntry + 1) + bufferLength);
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

        ~UnmanagedRioBuffer()
        {
            ReleaseUnmanagedResources();
        }
    }
}
