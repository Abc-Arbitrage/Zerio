using System;

namespace Abc.Zerio.Core
{
    public class CompletionQueues : IDisposable
    {
        public CompletionQueues(ZerioConfiguration configuration)
        {
            SendingQueue = new RioCompletionQueue(configuration.SendingBufferCount + configuration.ReceivingBufferCount * configuration.SessionCount);
            ReceivingQueue = new RioCompletionQueue(configuration.SendingBufferCount + configuration.ReceivingBufferCount * configuration.SessionCount);
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


