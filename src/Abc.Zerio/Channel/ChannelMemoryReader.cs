using System;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zerio.Channel
{
    internal unsafe class ChannelMemoryReader
    {
        private readonly ChannelMemoryPartition[] _partitions;
        private readonly long _partitionDataCapacity;
        private long _readPosition;

        public IReadOnlyCollection<ChannelMemoryPartition> Partitions => _partitions;

        public ChannelMemoryReader(ChannelMemoryPartitionGroup partitions)
        {
            _partitions = partitions.ToArray();
            _partitionDataCapacity = _partitions[0].DataCapacity;
        }

        public FrameBlock TryReadNextFrame()
        {
            while (true)
            {
                var readPosition = _readPosition;

                var partitionIndex = Math.DivRem(readPosition, _partitionDataCapacity, out var readOffsetInPartition);
                var partition = _partitions[partitionIndex];

                if (!partition.IsReadyToRead)
                    return FrameBlock.Empty;

                var frameLength = *(long*)(partition.DataPointer + readOffsetInPartition);

                if (frameLength > 0)
                {
                    _readPosition += frameLength;
                    return new FrameBlock(partition.DataPointer + readOffsetInPartition, frameLength);
                }

                switch (frameLength)
                {
                    case 0:
                        return FrameBlock.Empty;

                    case FrameBlock.EndOfPartitionMarker:
                        var nextPartitionIndex = (partitionIndex + 1) % _partitions.Length;
                        _readPosition = nextPartitionIndex * _partitionDataCapacity;
                        partition.MarkAsReadEnded();
                        continue;

                    default:
                        _readPosition += -frameLength;
                        continue;
                }
            }
        }
    }
}
