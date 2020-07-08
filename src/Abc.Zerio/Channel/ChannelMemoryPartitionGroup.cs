using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zerio.Channel
{
    internal class ChannelMemoryPartitionGroup : IReadOnlyList<ChannelMemoryPartition>
    {
        private readonly ChannelMemoryPartition[] _partitions;

        public ChannelMemoryPartitionGroup(RegisteredMemoryChannelBuffer buffer, int partitionCount, int offset, int partitionSize, bool init)
        {
            if (partitionCount < 1)
                throw new ArgumentException("Partitions cannot be empty", nameof(partitionCount));

            _partitions = new ChannelMemoryPartition[partitionCount];

            for (var i = 0; i < partitionCount; ++i)
            {
                var partition = _partitions[i] = new ChannelMemoryPartition(buffer, offset, partitionSize);
                offset += partitionSize;

                if (init)
                    partition.Init(i == 0);
            }
        }

        public int Count => _partitions.Length;
        public ChannelMemoryPartition this[int index] => _partitions[index];

        public IEnumerator<ChannelMemoryPartition> GetEnumerator() => _partitions.AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
