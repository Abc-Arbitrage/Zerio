using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Channel
{
    internal unsafe class ChannelMemoryBuffer : IDisposable
    {
        private const int _partitionCount = 3;
        private const int _mainPartitionSize = 32 * 1024 * 1024;

        private byte* _dataPointer;
        private IntPtr _buffer;

        public ChannelMemoryBuffer(int partitionSize)
        {
            _dataPointer = AllocateBuffer(partitionSize);

            // The pointer is always aligned to the system allocation granularity, the assertion below should never fire
            if (!MemoryUtil.IsAlignedToCacheLine((long)_dataPointer))
                throw new InvalidOperationException("Invalid shared memory alignment");

            ProducerPartitionGroup = new ChannelMemoryPartitionGroup(this, _partitionCount, 0, _mainPartitionSize, true);
            ConsumerPartitionGroup = new ChannelMemoryPartitionGroup(this, _partitionCount, 0, _mainPartitionSize, false);
        }

        public ChannelMemoryPartitionGroup ConsumerPartitionGroup { get; set; }
        public ChannelMemoryPartitionGroup ProducerPartitionGroup { get; set; }

        public byte* DataPointer => _dataPointer;

        private byte* AllocateBuffer(int partitionSize)
        {
            var bufferSize = _partitionCount * partitionSize;
            
            const int allocationType = Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE;
            _buffer = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)bufferSize, allocationType, Kernel32.Consts.PAGE_READWRITE);
            if (_buffer == IntPtr.Zero)
                WinSock.ThrowLastWsaError();

            return (byte*)_buffer.ToPointer();
        }
        
        public void Dispose()
        {
            if (_dataPointer != null)
            {
                Kernel32.VirtualFree(_buffer, 0, Kernel32.Consts.MEM_RELEASE);
                _dataPointer = null;
            }
        }
    }
}
