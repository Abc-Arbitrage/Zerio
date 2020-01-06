using System;

namespace Abc.Zerio.Core
{
    public class CompletionQueues : IDisposable
    {
        public CompletionQueues(ZerioConfiguration configuration)
        {
            SendingQueue = new RioCompletionQueue((configuration.MaxSendCompletionResults + configuration.MaxReceiveCompletionResults) * configuration.SessionCount * 2);
            ReceivingQueue = new RioCompletionQueue((configuration.MaxSendCompletionResults + configuration.MaxReceiveCompletionResults) * configuration.SessionCount * 2);
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


