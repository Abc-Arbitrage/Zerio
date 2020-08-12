namespace Abc.Zerio.Core
{
    public class InternalZerioConfiguration
    {
        public int MaxSendCompletionResults { get; set; }
        public int MaxReceiveCompletionResults { get; set; }
        
        public int ReceivingBufferSegmentCount { get; set; }
        public int ReceivingBufferSegmentLength { get; set; }
        
        public int SessionCount { get; set; }
        public int FramingBufferLength { get; set; }

        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        public int RequestQueueMaxOutstandingReceives { get; set; }
        public int RequestQueueMaxOutstandingSends { get; set; }
        
        public int SendingCompletionQueueSize { get; set; }
        public int ReceivingCompletionQueueSize { get; set; }
        public bool BatchFramesOnSend { get; set; }
        public int MaxFrameBatchSize { get; set; }
        
        public int SendingBufferLength { get; set; }
    }
}
