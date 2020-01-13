using System;
using System.Runtime.ConstrainedExecution;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    public sealed unsafe class RioCompletionQueue : CriticalFinalizerObject, IDisposable
    {
        public IntPtr QueueHandle { get; }

        public RioCompletionQueue(int size)
        {
            // we want to handle the polling manually, so we don't have to pass any RIO_NOTIFICATION_COMPLETION
            QueueHandle = WinSock.Extensions.CreateCompletionQueue((uint)size);
            if (QueueHandle == IntPtr.Zero)
                WinSock.ThrowLastWsaError();
        }

        internal int TryGetCompletionResults(RIO_RESULT* results, int maxCompletionResults)
        {
            var completionQueue = QueueHandle;

            var resultCount = WinSock.Extensions.DequeueCompletion(completionQueue, results, (uint)maxCompletionResults);

            if (resultCount == WinSock.Consts.RIO_CORRUPT_CQ)
                WinSock.ThrowLastWsaError();

            return (int)resultCount;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool _)
        {
            if (QueueHandle != IntPtr.Zero)
                WinSock.Extensions.CloseCompletionQueue(QueueHandle);
        }

        ~RioCompletionQueue()
        {
            Dispose(false);
        }
    }
}
