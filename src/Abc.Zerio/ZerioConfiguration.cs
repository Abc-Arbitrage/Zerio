using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public abstract class ZerioConfiguration
    {
        public int SendingBufferCount { get; set; }
        public int SendingBufferLength { get; set; }
        public bool BatchSendRequests { set; get; }
        public int MaxSendBatchSize { get; set; }
        public bool ConflateSendRequestsOnProcessing { get; set; }
        public bool ConflateSendRequestsOnEnqueuing { get; set; }
        public int MaxConflatedSendRequestCount { get; set; }

        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int FramingBufferLength { get; set; }

        public RequestEngineWaitStrategyType RequestEngineWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        protected ZerioConfiguration()
        {
            BatchSendRequests = true;
            ConflateSendRequestsOnProcessing = true;
            ConflateSendRequestsOnEnqueuing = true;

            MaxConflatedSendRequestCount = 8;
            MaxSendBatchSize = 8;
            SendingBufferLength = 4096;
            SendingBufferCount = 64 * 1024;

            FramingBufferLength = 64 * 1024;
            ReceivingBufferLength = 64 * 1024;
            ReceivingBufferCount = 256;

            RequestEngineWaitStrategyType = RequestEngineWaitStrategyType.BusySpinWaitStrategy;
            ReceiveCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
            SendCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
        }

        internal virtual InternalZerioConfiguration ToInternalConfiguration()
        {
            // const int allocationGranularity = 65536;
            
            var configuration = new InternalZerioConfiguration
            {
                MaxSendBatchSize = MaxSendBatchSize,
                SendingBufferLength = SendingBufferLength,
                SendingBufferCount = SendingBufferCount,
                ReceivingBufferLength = ReceivingBufferLength,
                ReceivingBufferCount = ReceivingBufferCount,
                RequestEngineWaitStrategyType = RequestEngineWaitStrategyType,
                ReceiveCompletionPollingWaitStrategyType = ReceiveCompletionPollingWaitStrategyType,
                SendCompletionPollingWaitStrategyType = SendCompletionPollingWaitStrategyType,
                FramingBufferLength = ReceivingBufferLength,
            };

            var sendBufferCount = InternalZerioConfiguration.GetNextPowerOfTwo(configuration.SendingBufferCount);
            
            configuration.RequestQueueMaxOutstandingSends = sendBufferCount;
            configuration.SendingCompletionQueueSize = sendBufferCount;
            configuration.MaxSendCompletionResults = sendBufferCount;
            configuration.SendRequestProcessingEngineRingBufferSize = sendBufferCount;
            
            configuration.BatchSendRequests = BatchSendRequests; 
            configuration.ConflateSendRequestsOnProcessing = ConflateSendRequestsOnProcessing;
            configuration.ConflateSendRequestsOnEnqueuing = ConflateSendRequestsOnEnqueuing;
            configuration.MaxConflatedSendRequestCount = MaxConflatedSendRequestCount;
    
            configuration.MaxReceiveCompletionResults = ReceivingBufferCount;
            configuration.RequestQueueMaxOutstandingReceives = ReceivingBufferCount;
            configuration.ReceivingCompletionQueueSize = ReceivingBufferCount;

            return configuration;
        }
    }
}