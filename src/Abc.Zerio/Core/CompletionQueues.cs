using System;

namespace Abc.Zerio.Core
{
    public class CompletionQueues : IDisposable
    {
        public CompletionQueues(ZerioConfiguration configuration)
        {
            SendingQueue = new RioCompletionQueue(configuration.SendingBufferCount);
            ReceivingQueue = new RioCompletionQueue(configuration.ReceivingBufferCount);
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


