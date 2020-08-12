using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public abstract class ZerioConfiguration
    {
        public int ReceivingBufferSegmentCount { get; set; }
        public int ReceivingBufferSegmentLength { get; set; }
        public int FramingBufferLength { get; set; }

        public int MaxFrameBatchSize { get; set; }
        public bool BatchFramesOnSend { get; set; }

        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        protected ZerioConfiguration()
        {
            FramingBufferLength = 64 * 1024;
            ReceivingBufferSegmentLength = 1 * 1024 * 1024;
            ReceivingBufferSegmentCount = 256;
            
            BatchFramesOnSend = false;
            MaxFrameBatchSize = int.MaxValue;
            
            ReceiveCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
            SendCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
        }

        internal virtual InternalZerioConfiguration ToInternalConfiguration()
        {
            // const int allocationGranularity = 65536;

            var configuration = new InternalZerioConfiguration
            {
                ReceivingBufferSegmentLength = ReceivingBufferSegmentLength,
                ReceivingBufferSegmentCount = ReceivingBufferSegmentCount,
                ReceiveCompletionPollingWaitStrategyType = ReceiveCompletionPollingWaitStrategyType,
                SendCompletionPollingWaitStrategyType = SendCompletionPollingWaitStrategyType,
                FramingBufferLength = FramingBufferLength,
                MaxReceiveCompletionResults = ReceivingBufferSegmentCount,
                
                RequestQueueMaxOutstandingReceives = ReceivingBufferSegmentCount,
                RequestQueueMaxOutstandingSends = 65_536,
                
                ReceivingCompletionQueueSize = ReceivingBufferSegmentCount,
                BatchFramesOnSend = BatchFramesOnSend,
                MaxFrameBatchSize = MaxFrameBatchSize,
                
                SendingCompletionQueueSize = 65_536,
                MaxSendCompletionResults = 256,
                SendingBufferLength = 5_048_576,
            };

            return configuration;
        }
    }
}