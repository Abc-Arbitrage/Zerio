using System;
using System.Threading;
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
            if(QueueHandle.IsInvalid)
                WinSock.ThrowLastWsaError();
        }

        internal int TryGetCompletionResults(CancellationToken cancellationToken, RIO_RESULT* results, int maxCompletionResults)
        {
            var completionQueue = QueueHandle;

            var spinwait = new SpinWait();
            while (!cancellationToken.IsCancellationRequested)
            {
                var resultCount = WinSock.Extensions.DequeueCompletion(completionQueue, results, (uint)maxCompletionResults);

                if (resultCount == 0)
                {
                    spinwait.SpinOnce();
                    continue;
                }

                if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                {
                    WinSock.ThrowLastWsaError();
                    break;
                }

                return (int)resultCount;
            }

            return 0;
        }

        public void Dispose()
        {
            if (QueueHandle != null && !QueueHandle.IsInvalid)
                QueueHandle.Dispose();
        }
    }
}
