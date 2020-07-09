using System;
using System.Linq;
using System.Threading;

namespace Abc.Zerio.Channel
{
    internal unsafe class RegisteredMemoryChannelWriter
    {
        private readonly ChannelMemoryPartition[] _partitions;
        private readonly long _partitionDataCapacity;
        private readonly long _maxFrameLength;

        private long _writePosition;

        public RegisteredMemoryChannelWriter(ChannelMemoryPartitionGroup partitions)
        {
            _partitions = partitions.ToArray();
            _writePosition = 0;

            _partitionDataCapacity = _partitions[0].DataCapacity;
            _maxFrameLength = _partitionDataCapacity - FrameBlock.EndOfPartitionMarkerSize;
        }

        public FrameBlock AcquireFrame(long messageLength)
        {
            var frameLength = FrameBlock.GetFrameLength(messageLength);
            if (frameLength > _maxFrameLength)
                return FrameBlock.Empty;

            // var spinWait = new SpinWait();

            while (true)
            {
                var writePosition = Volatile.Read(ref _writePosition);

                var partitionIndex = Math.DivRem(writePosition, _partitionDataCapacity, out var writeOffsetInPartition);
                var partition = _partitions[partitionIndex];

                if (!partition.IsReadyToWrite)
                {
                    // spinWait.SpinOnce();
                    continue;
                }

                var hasRoomForMessage = writeOffsetInPartition + frameLength <= _maxFrameLength;
                if (hasRoomForMessage)
                {
                    if (Interlocked.CompareExchange(ref _writePosition, writePosition + frameLength, writePosition) == writePosition)
                        return new FrameBlock(partition.DataPointer + writeOffsetInPartition, frameLength);
                }
                else
                {
                    var nextPartitionIndex = (partitionIndex + 1) % _partitions.Length;
                    var nextPartition = _partitions[nextPartitionIndex];

                    if (!nextPartition.IsReadyToStartWrite || !partition.IsReadyToEndWrite)
                    {
                        // spinWait.SpinOnce();
                        continue;
                    }

                    if (Interlocked.CompareExchange(ref _writePosition, nextPartitionIndex * _partitionDataCapacity, writePosition) == writePosition)
                    {
                        nextPartition.MarkAsWriteStarted();
                        partition.MarkAsWriteEnded();
                        WriteEndOfPartition(partition, writeOffsetInPartition);
                    }
                }
            }
        }

        private static void WriteEndOfPartition(ChannelMemoryPartition partition, long endOffsetInPartition)
        {
            Volatile.Write(ref *(long*)(partition.DataPointer + endOffsetInPartition), FrameBlock.EndOfPartitionMarker);
        }
    }
}
