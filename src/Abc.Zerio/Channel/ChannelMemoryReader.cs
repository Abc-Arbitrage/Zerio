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

        public event Action<FrameBlock, bool, bool> FrameRead;

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
                    return TryFlushBatch(false);

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
                        return TryFlushBatch(false);

                    case FrameBlock.EndOfPartitionMarker:
                        var nextPartitionIndex = (partitionIndex + 1) % _partitions.Length;
                        _readPosition = nextPartitionIndex * _partitionDataCapacity;
                        partition.MarkAsReadEnded();
                        TryFlushBatch(true);
                        continue;

                    default:
                        _readPosition += -frameLength;
                        continue;
                }
            }
        }

        private bool TryFlushBatch(bool endOfPartition)
        {
            if (_currentBatch.Count == 0)
                return false;

            for (var i = 0; i < _currentBatch.Count; i++)
            {
                var endOfBatch = i == _currentBatch.Count - 1;
                
                if(!endOfBatch)
                    FrameRead?.Invoke(_currentBatch[i], false, false);
                else
                    FrameRead?.Invoke(_currentBatch[i], true, endOfPartition);
            }

            _currentBatch.Clear();
            return true;
        }

        public void CleanupPartition()
        {
            foreach (var memoryPartition in _partitions)
            {
                memoryPartition.CleanIfRequired();
            }
        }
    }
}
