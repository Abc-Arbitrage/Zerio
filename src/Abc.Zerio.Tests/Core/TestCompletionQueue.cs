using System;
using System.Collections.Concurrent;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Tests.Core
{
    internal class TestCompletionQueue : IRioCompletionQueue
    {
        public ConcurrentQueue<RIO_RESULT[]> AvailableResults = new ConcurrentQueue<RIO_RESULT[]>();

        public unsafe int TryGetCompletionResults(RIO_RESULT* results, int maxCompletionResults)
        {
            if (!AvailableResults.TryDequeue(out var resultArray))
                return 0;

            var upperBound = Math.Min(resultArray.Length, maxCompletionResults);
            for (var i = 0; i < upperBound; i++)
            {
                results[i] = resultArray[i];
            }

            return upperBound;
        }

        public CompletionQueueHandle QueueHandle { get; }

        public void Dispose()
        {
        }
    }
}
