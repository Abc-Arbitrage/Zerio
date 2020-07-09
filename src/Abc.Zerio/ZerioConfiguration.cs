using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public abstract class ZerioConfiguration
    {
        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int FramingBufferLength { get; set; }

        public CompletionPollingWaitStrategyType ReceiveCompletionPollingWaitStrategyType { get; set; }
        public CompletionPollingWaitStrategyType SendCompletionPollingWaitStrategyType { get; set; }

        protected ZerioConfiguration()
        {
            FramingBufferLength = 32 * 1024 * 1024;
            ReceivingBufferLength = 1 * 1024 * 1024;
            ReceivingBufferCount = 256;

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
                ReceivingCompletionQueueSize = ReceivingBufferCount,
                RequestQueueMaxOutstandingSends = 65536,
                SendingCompletionQueueSize = 65536,
                MaxSendCompletionResults = 65536,
            };

            return configuration;
        }
    }
}