using System;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zerio.Channel
{
    internal unsafe class RegisteredMemoryChannelReader
    {
        private readonly int? _maxBatchSize;
        private readonly ChannelMemoryPartition[] _partitions;
        private readonly long _partitionDataCapacity;
        private long _readPosition;

        public event Action<FrameBlock, bool, bool> FrameRead;

        public RegisteredMemoryChannelReader(ChannelMemoryPartitionGroup partitions, int maxBatchSize)
        {
            _maxBatchSize = maxBatchSize;
            _partitions = partitions.ToArray();
            _partitionDataCapacity = _partitions[0].DataCapacity;
        }

        private readonly List<FrameBlock> _currentBatch = new List<FrameBlock>(8192);
        private bool _hasPendingCleaningRequest;

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

                    if (_currentBatch.Count == _maxBatchSize)
                        TryFlushBatch(false);
                        
                    continue;
                }

                switch (frameLength)
                {
                    // No more data available, we need to flush the batch
                    case 0:
                        return TryFlushBatch(false);

                    // End of partition, we need to end the batch because frames can't be aggregated across partitions
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
            {
                if(endOfPartition)
                    _hasPendingCleaningRequest = true;
                
                return false;
            }

            if (_hasPendingCleaningRequest)
            {
                endOfPartition = true;
                _hasPendingCleaningRequest = false;
            }

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
