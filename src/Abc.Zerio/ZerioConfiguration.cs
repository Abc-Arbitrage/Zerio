using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public abstract class ZerioConfiguration
    {
        public int SendingBufferCount { get; set; }
        public int SendingBufferLength { get; set; }
        public int MaxSendBatchSize { get; set; }
        public bool BatchRequests { get; set; }

        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int FramingBufferLength { get; set; }
        
        public RequestEngineWaitStrategyType RequestEngineWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        protected ZerioConfiguration()
        {
            BatchRequests = true;
            
            MaxSendBatchSize = 16;
            SendingBufferLength = 1024;
            SendingBufferCount = 64 * 1024;

            FramingBufferLength = 64 * 1024;
            ReceivingBufferLength = 64 * 1024;
            ReceivingBufferCount = 256;

            RequestEngineWaitStrategyType = RequestEngineWaitStrategyType.HybridWaitStrategy;
            ReceiveCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;
            SendCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.SpinWaitWaitStrategy;
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
                
                MaxSendCompletionResults = SendingBufferCount,
                MaxReceiveCompletionResults = ReceivingBufferCount,
                
                BatchRequests = BatchRequests,
            };

            configuration.RequestQueueMaxOutstandingReceives = configuration.ReceivingBufferCount * 2;
            configuration.RequestQueueMaxOutstandingSends = configuration.SendingBufferCount * 2;
            
            configuration.SendingCompletionQueueSize = (configuration.MaxSendCompletionResults + configuration.MaxReceiveCompletionResults) * 2;
            configuration.ReceivingCompletionQueueSize = (configuration.MaxSendCompletionResults + configuration.MaxReceiveCompletionResults) *  2;

            configuration.RequestProcessingEngineRingBufferSize = configuration.SendingBufferCount + configuration.ReceivingBufferCount;
            
            return configuration;
        }
    }
}