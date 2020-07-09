namespace Abc.Zerio.Core
{
    public class InternalZerioConfiguration
    {
        public int MaxSendCompletionResults { get; set; }

        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int MaxReceiveCompletionResults { get; set; }

        public int SessionCount { get; set; }
        public int FramingBufferLength { get; set; }

        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        public int RequestQueueMaxOutstandingReceives { get; set; }
        public int RequestQueueMaxOutstandingSends { get; set; }
        
        public int SendingCompletionQueueSize { get; set; }
        public int ReceivingCompletionQueueSize { get; set; }
    }
}
