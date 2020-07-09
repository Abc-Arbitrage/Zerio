using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Channel
{
    internal unsafe class RegisteredMemoryChannelBuffer : IDisposable
    {
        private const int _partitionCount = 3;

        private byte* _dataPointer;
        private IntPtr _buffer;
        private IntPtr _bufferId;

        public RegisteredMemoryChannelBuffer(int partitionSize)
        {
            _dataPointer = AllocateBuffer(partitionSize);

            // The pointer is always aligned to the system allocation granularity, the assertion below should never fire
            if (!MemoryUtil.IsAlignedToCacheLine((long)_dataPointer))
                throw new InvalidOperationException("Invalid shared memory alignment");

            ProducerPartitionGroup = new ChannelMemoryPartitionGroup(this, _partitionCount, 0, partitionSize, true);
            ConsumerPartitionGroup = new ChannelMemoryPartitionGroup(this, _partitionCount, 0, partitionSize, false);
        }

        public ChannelMemoryPartitionGroup ConsumerPartitionGroup { get; set; }
        public ChannelMemoryPartitionGroup ProducerPartitionGroup { get; set; }

        public byte* DataPointer => _dataPointer;
        
        public RIO_BUF CreateBufferSegmentDescriptor(ChannelFrame frame)
        {
            var bufferSegmentDescriptor = new RIO_BUF
            {
                BufferId = _bufferId,
                Length = (int)frame.DataLength, 
                Offset = (int)(frame.DataPosition - _dataPointer)
            };
            
            return bufferSegmentDescriptor;
        }
        
        private byte* AllocateBuffer(int partitionSize)
        {
            var bufferSize = (uint)(_partitionCount * partitionSize);
            
            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            _buffer = Kernel32.VirtualAlloc(IntPtr.Zero, bufferSize, allocationType, Kernel32.Consts.PAGE_READWRITE);
            if (_buffer == IntPtr.Zero)
                WinSock.ThrowLastWsaError();

            WinSock.EnsureIsInitialized();
            
            _bufferId = WinSock.Extensions.RegisterBuffer(_buffer, bufferSize);
            if (_bufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();
            
            return (byte*)_buffer.ToPointer();
        }
        
        public void Dispose()
        {
            if (_dataPointer != null)
            {
                try
                {
                    WinSock.Extensions.DeregisterBuffer(_bufferId);
                }
                finally
                {
                    Kernel32.VirtualFree(_buffer, 0, Kernel32.Consts.MEM_RELEASE);
                }
                _dataPointer = null;
            }
        }
    }
}
