using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public class ZerioConfiguration
    {
        public int SendingBufferSegmentCount { get; set; }
        public int SendingBufferSegmentLength { get; set; }
        public int MaxSendCompletionResults { get; set; }

        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferSegmentLength { get; set; }
        public int MaxReceiveCompletionResults { get; set; }

        public int MaxSendBatchSize { get; set; }
        public int SessionCount { get; set; }
        public int FramingBufferLength { get; set; }

        public RequestEngineWaitStrategyType RequestEngineWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        public static ZerioConfiguration CreateDefault()
        {
            // const int allocationGranularity = 65536;
            var configuration = new ZerioConfiguration
            {
                MaxSendBatchSize = 16,
                SessionCount = 1,
                
                SendingBufferSegmentLength = 1024,
                SendingBufferSegmentCount = 64 * 1024,
                
                ReceivingBufferSegmentLength = 1024,
                ReceivingBufferCount = 1024,
                
                RequestEngineWaitStrategyType = RequestEngineWaitStrategyType.HybridWaitStrategy,
                ReceiveCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy,
                SendCompletionPollingWaitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy,
            };

            configuration.FramingBufferLength = configuration.ReceivingBufferSegmentLength;
            configuration.MaxSendCompletionResults = configuration.SendingBufferSegmentCount;
            configuration.MaxReceiveCompletionResults = configuration.ReceivingBufferCount;

            return configuration;
        }

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
