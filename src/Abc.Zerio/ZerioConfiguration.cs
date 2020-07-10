using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public abstract class ZerioConfiguration
    {
        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int FramingBufferLength { get; set; }

        public int MaxFrameBatchSize { get; set; }
        public bool BatchFramesOnSend { get; set; }

        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        protected ZerioConfiguration()
        {
            FramingBufferLength = 64 * 1024;
            ReceivingBufferLength = 1 * 1024 * 1024;
            ReceivingBufferCount = 256;
            
            BatchFramesOnSend = true;
            MaxFrameBatchSize = int.MaxValue;
            
            ReceiveCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
            SendCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.SpinWaitWaitStrategy;
        }

        internal virtual InternalZerioConfiguration ToInternalConfiguration()
        {
            // const int allocationGranularity = 65536;

            var configuration = new InternalZerioConfiguration
            {
                ReceivingBufferLength = ReceivingBufferLength,
                ReceivingBufferCount = ReceivingBufferCount,
                ReceiveCompletionPollingWaitStrategyType = ReceiveCompletionPollingWaitStrategyType,
                SendCompletionPollingWaitStrategyType = SendCompletionPollingWaitStrategyType,
                FramingBufferLength = FramingBufferLength,
                MaxReceiveCompletionResults = ReceivingBufferCount,
                
                RequestQueueMaxOutstandingReceives = ReceivingBufferCount,
                RequestQueueMaxOutstandingSends = 65_536,
                
                ReceivingCompletionQueueSize = ReceivingBufferCount,
                BatchFramesOnSend = BatchFramesOnSend,
                MaxFrameBatchSize = MaxFrameBatchSize,
                
                SendingCompletionQueueSize = 65_536,
                MaxSendCompletionResults = 65_536,
                ChannelPartitionSize = 2_554_432,
            };

            return configuration;
        }
    }
}