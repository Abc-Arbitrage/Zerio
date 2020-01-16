using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal unsafe interface IRioCompletionQueue : IDisposable
    {
        int TryGetCompletionResults(RIO_RESULT* results, int maxCompletionResults);
        CompletionQueueHandle QueueHandle { get; }
    }
}
