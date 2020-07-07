using System;
using System.Threading;

namespace Abc.Zerio.Channel
{
    internal unsafe class ChannelMemoryPartition
    {
        private const int _statusOffset = 0;

        // Should be a multiple of 8 to ensure that frames are aligned and that the length values are aligned
        private const int _dataOffset = _statusOffset + MemoryUtil.CacheLineLength;

        public byte* HeaderPointer { get; }
        public byte* DataPointer { get; }

        public int PartitionSize { get; }
        public int DataCapacity => PartitionSize - _dataOffset;

        public event Action ReadComplete;

        public ChannelMemoryPartition(ChannelMemoryBuffer buffer, long offset, int size)
        {
            if (sizeof(IntPtr) < sizeof(long))
                throw new InvalidOperationException("Buy some real hardware dude");
            
            if (!MemoryUtil.IsAlignedToCacheLine(offset))
                throw new ArgumentException("Offset must be aligned to a cache line", nameof(offset));
            
            if (!MemoryUtil.IsAlignedToCacheLine(size))
                throw new ArgumentException($"Partition size must be a multiple of {MemoryUtil.CacheLineLength}", nameof(offset));
            
            if (size <= _dataOffset)
                throw new ArgumentException("Partition size is too small", nameof(size));

            HeaderPointer = buffer.DataPointer + offset;
            DataPointer = HeaderPointer + _dataOffset;
            PartitionSize = size;
        }

        public void Init(bool isFirstPartitionInGroup)
        {
            Status = isFirstPartitionInGroup ? PartitionStatus.ReadWrite : PartitionStatus.Clean;
        }

        internal PartitionStatus Status
        {
            get => (PartitionStatus)Volatile.Read(ref *(int*)(HeaderPointer + _statusOffset));
            private set => Volatile.Write(ref *(int*)(HeaderPointer + _statusOffset), (int)value);
        }

        public bool IsReadyToRead
        {
            get
            {
                var status = Status;
                return status == PartitionStatus.ReadWrite || status == PartitionStatus.ReadOnly;
            }
        }

        public bool IsReadyToWrite
        {
            get
            {
                var status = Status;
                return status == PartitionStatus.ReadWrite || status == PartitionStatus.Clean;
            }
        }

        public bool IsReadyToStartWrite => Status == PartitionStatus.Clean;
        public bool IsReadyToEndWrite => Status == PartitionStatus.ReadWrite;

        public void MarkAsWriteStarted()
        {
            SwitchStatus(PartitionStatus.ReadWrite, PartitionStatus.Clean);
        }

        public void MarkAsWriteEnded()
        {
            SwitchStatus(PartitionStatus.ReadOnly, PartitionStatus.ReadWrite);
        }

        public void MarkAsReadEnded()
        {
            SwitchStatus(PartitionStatus.ToClean, PartitionStatus.ReadOnly);
            ReadComplete?.Invoke();
        }

        public void CleanIfRequired()
        {
            if (Status != PartitionStatus.ToClean)
                return;

            MemoryUtil.ZeroMemory((IntPtr)DataPointer, (IntPtr)DataCapacity);
            SwitchStatus(PartitionStatus.Clean, PartitionStatus.ToClean);
        }

        private void SwitchStatus(PartitionStatus newStatus, PartitionStatus prevStatus)
        {
            var readStatus = (PartitionStatus)Interlocked.CompareExchange(ref *(int*)(HeaderPointer + _statusOffset), (int)newStatus, (int)prevStatus);
            if (readStatus != prevStatus)
                throw new InvalidOperationException($"Unexpected partition status when switching to {newStatus}: expected {prevStatus} but was {readStatus}");
        }

        internal enum PartitionStatus
        {
            ReadWrite,
            ReadOnly,
            ToClean,
            Clean
        }
    }
}
