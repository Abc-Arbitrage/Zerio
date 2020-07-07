using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Abc.Zerio.Channel
{
    internal class ChannelMemoryCleaner : IDisposable
    {
        private readonly ChannelMemoryPartition[] _partitions;
        private readonly Thread _cleanThread;
        private readonly AutoResetEvent _cleanSignal = new AutoResetEvent(true);

        private volatile bool _isRunning = true;

        public ChannelMemoryCleaner(IEnumerable<ChannelMemoryPartition> partitions)
        {
            _partitions = partitions.ToArray();

            foreach (var partition in _partitions)
            {
                partition.ReadComplete += OnPartitionRead;
            }

            _cleanThread = new Thread(CleanThread){IsBackground = true};
            _cleanThread.Start();
        }

        private void OnPartitionRead()
        {
            _cleanSignal.Set();
        }

        private void CleanThread()
        {
            while (_isRunning)
            {
                _cleanSignal.WaitOne();

                foreach (var memoryPartition in _partitions)
                {
                    memoryPartition.CleanIfRequired();
                }
            }
        }

        public void Dispose()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            foreach (var partition in _partitions)
            {
                partition.ReadComplete -= OnPartitionRead;
            }

            _cleanSignal.Set();
            _cleanThread.Join();
            _cleanSignal.Dispose();
        }
    }
}
