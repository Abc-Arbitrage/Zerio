using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    public sealed unsafe class RioCompletionQueue : IDisposable
    {
        public CompletionQueueHandle QueueHandle { get; }

        public RioCompletionQueue(int size)
        {
            // we want to handle the polling manually, so we don't have to pass any RIO_NOTIFICATION_COMPLETION
            QueueHandle = WinSock.Extensions.CreateCompletionQueue((uint)size);
            if (QueueHandle.IsInvalid)
                WinSock.ThrowLastWsaError();
        }

        internal int TryGetCompletionResults(RIO_RESULT* results, int maxCompletionResults)
        {
            var completionQueue = QueueHandle;

            var resultCount = WinSock.Extensions.DequeueCompletion(completionQueue.DangerousGetHandle(), results, (uint)maxCompletionResults);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            return (int)resultCount;
        }

        public void Dispose()
        {
            if (QueueHandle != null && !QueueHandle.IsInvalid)
                QueueHandle.Dispose();
        }
    }
}
