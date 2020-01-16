using System;

namespace Abc.Zerio.Core
{
    internal class CompletionQueues : IDisposable
    {
        public CompletionQueues(InternalZerioConfiguration configuration)
        {
            SendingQueue = new RioCompletionQueue(configuration.SendingCompletionQueueSize);
            ReceivingQueue = new RioCompletionQueue(configuration.ReceivingCompletionQueueSize);
        }

        public IRioCompletionQueue SendingQueue { get; }
        public IRioCompletionQueue ReceivingQueue { get; }

        public void Dispose()
        {
            SendingQueue.Dispose();
            ReceivingQueue.Dispose();
        }
    }
}


