using System;
using System.Threading;

namespace Abc.Zerio.Core
{
    internal static class CompletionPollingWaitStrategyFactory
    {
        public static ICompletionPollingWaitStrategy Create(CompletionPollingWaitStrategyType waitStrategyType)
        {
            return waitStrategyType switch
            {
                CompletionPollingWaitStrategyType.BusySpinWaitStrategy => new BusySpinCompletionPollingWaitStrategy(),
                CompletionPollingWaitStrategyType.SpinWaitWaitStrategy => new SpinWaitCompletionPollingWaitStrategy(new SpinWait()),
                _                                                      => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
