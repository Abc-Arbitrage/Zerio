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
        public event Action<FrameBlock, bool> FrameRead;

        public ChannelMemoryReader(ChannelMemoryPartitionGroup partitions)
        {
            _partitions = partitions.ToArray();
            _partitionDataCapacity = _partitions[0].DataCapacity;
        }

        private readonly List<FrameBlock> _currentBatch = new List<FrameBlock>(256);

        public bool TryReadFrameBatch()
        {
            while (true)
            {
                var readPosition = _readPosition;

                var partitionIndex = Math.DivRem(readPosition, _partitionDataCapacity, out var readOffsetInPartition);
                var partition = _partitions[partitionIndex];

                if (!partition.IsReadyToRead)
                    return TryRaiseBatch();

                var frameLength = *(long*)(partition.DataPointer + readOffsetInPartition);

                if (frameLength > 0)
                {
                    _readPosition += frameLength;
                    _currentBatch.Add(new FrameBlock(partition.DataPointer + readOffsetInPartition, frameLength));
                    continue;
                }

                switch (frameLength)
                {
                    case 0:
                        return TryRaiseBatch();

                    case FrameBlock.EndOfPartitionMarker:
                        var nextPartitionIndex = (partitionIndex + 1) % _partitions.Length;
                        _readPosition = nextPartitionIndex * _partitionDataCapacity;
                        TryRaiseBatch();
                        partition.MarkAsReadEnded();
                        continue;

                    default:
                        _readPosition += -frameLength;
                        continue;
                }
            }
        }

        private bool TryRaiseBatch()
        {
            if (_currentBatch.Count == 0)
                return false;

            for (var i = 0; i < _currentBatch.Count; i++)
            {
                FrameRead?.Invoke(_currentBatch[i], i == _currentBatch.Count - 1);
            }

            _currentBatch.Clear();
            return true;
        }
    }
}
