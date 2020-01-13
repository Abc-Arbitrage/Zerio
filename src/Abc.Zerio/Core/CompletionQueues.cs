using System;

namespace Abc.Zerio.Core
{
    public class CompletionQueues : IDisposable
    {
        public CompletionQueues(InternalZerioConfiguration configuration)
        {
            SendingQueue = new RioCompletionQueue(configuration.SendingCompletionQueueSize);
            ReceivingQueue = new RioCompletionQueue(configuration.ReceivingCompletionQueueSize);
        }

        public RioCompletionQueue SendingQueue { get; }
        public RioCompletionQueue ReceivingQueue { get; }

        public void Dispose()
        {
            SendingQueue.Dispose();
            ReceivingQueue.Dispose();
        }
    }
}


