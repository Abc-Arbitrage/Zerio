namespace Abc.Zerio.Core
{
    public class InternalZerioConfiguration
    {
        public int SendingBufferCount { get; set; }
        public int SendingBufferLength { get; set; }
        public int MaxSendCompletionResults { get; set; }

        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int MaxReceiveCompletionResults { get; set; }

        public int MaxSendBatchSize { get; set; }
        public int SessionCount { get; set; }
        public int FramingBufferLength { get; set; }

        public RequestEngineWaitStrategyType RequestEngineWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        public int RequestQueueMaxOutstandingReceives { get; set; }
        public int RequestQueueMaxOutstandingSends { get; set; }
        
        public int SendingCompletionQueueSize { get; set; }
        public int ReceivingCompletionQueueSize { get; set; }
        public int RequestProcessingEngineRingBufferSize { get; set; }
        public bool BatchRequests { get; set; }

        public static int GetNextPowerOfTwo(int value)
        {
            var powerOfTwo = 2;
            while (powerOfTwo < value)
            {
                powerOfTwo *= 2;
            }

            return powerOfTwo;
        }
    }
}
