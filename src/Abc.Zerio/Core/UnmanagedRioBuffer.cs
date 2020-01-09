using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal sealed unsafe class UnmanagedRioBuffer<T> : IDisposable
        where T : unmanaged, IRioBufferSegmentDescriptorContainer
    {
        private const int _padding = 128;
        
        private readonly IntPtr _buffer;
        private readonly IntPtr _bufferId;

        public T* FirstEntry { get; } 
        public int EntryReservedSpaceSize { get; }
        public int Length { get; }

        public T* this[int index] => (T*)((byte*)FirstEntry + index * EntryReservedSpaceSize);

        public UnmanagedRioBuffer(int bufferCount, int bufferLength)
        {
            // Example with:
            //  - Length = 2 entries
            //  - BufferLength = 1024 bytes
            //  - Entry of type T having
            //     - A 1 byte field
            //     - A 4 bytes field
            //     - A 4 bytes field
            //
            // |-- Padding --|- Entry reserved space --------------------------------------------------------|- Entry reserved space  -------------------------------------------------------|-- Padding --|   
            // |  128 bytes  |                                                                               |                                                                               |  128 bytes  |  
            // |             |- Entry #1 -----------------------------------------------|                    |- Entry #2 -----------------------------------------------|                    |             |  
            // |             |                                                          |                    |                                                          |                    |             |  
            // |             |           |- RIO_BUF ------------------------------------|- Data - ... -------|           |- RIO_BUF ------------------------------------|- Data - ... -------|             |  
            // |             |           |                                              |                    |           |                                              |                    |             |  
            //[0]           [128]       [137]                 [145]       [149]       [153]                [1177]      [1186]                 [1194]      [1198]      [1202]                 |             |  
            // |             +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+             |  
            // |             | Fields    | BufferId             | Offset    | Length    | Buffer segment     | Fields    | BufferId             | Offset    | Length    | Buffer segment     |             |  
            // |             |           | IntPtr               | int       | int       | bytes              |           | IntPtr               | int       | int       | bytes              |             |  
            // |             | 9 bytes   | 8 bytes              | 4         | 4         | 1024               | 9 bytes   | 8 bytes              | 4         | 4         | 1024               |             |  
            // |             +-----------+----------------------+-----------+-----------+-------- ... -------+-----------+----------------------+-----------+-----------+-------- ... -------+             | 
            // |             |                                  | 153       | 1024      |                                                       | 1202      | 1024      |               
            // |                                                  +---------------------^                                                         +---------------------^
            
            Length = bufferCount;
            var entryLength = sizeof(T);
            EntryReservedSpaceSize = entryLength + bufferLength;

            // Padding is added at the start and the end of the buffer to prevent false sharing when using this UnmanagedRioBuffer
            // as the underlying ring buffer memory of an unmanaged disruptor
            var totalBufferLength = (uint)(_padding + bufferCount * EntryReservedSpaceSize + _padding);

            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            _buffer = Kernel32.VirtualAlloc(IntPtr.Zero, totalBufferLength, allocationType, Kernel32.Consts.PAGE_READWRITE);
            if (_buffer == IntPtr.Zero)
                WinSock.ThrowLastWsaError();
            
            _bufferId = WinSock.Extensions.RegisterBuffer(_buffer, totalBufferLength);
            if (_bufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();

            var currentEntry = FirstEntry = (T*)((byte*)_buffer.ToPointer() + _padding);

            for (var i = 0; i < bufferCount; i++)
            {
                var bufferDescriptor = currentEntry->GetRioBufferDescriptor();
                
                bufferDescriptor->BufferId = _bufferId;
                bufferDescriptor->Offset = _padding + i * EntryReservedSpaceSize + sizeof(T);
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
