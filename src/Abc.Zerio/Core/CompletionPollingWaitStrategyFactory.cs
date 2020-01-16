using System;

namespace Abc.Zerio.Core
{
    internal static class CompletionPollingWaitStrategyFactory
    {
        public static ICompletionPollingWaitStrategy Create(CompletionPollingWaitStrategyType waitStrategyType)
        {
            return waitStrategyType switch
            {
                CompletionPollingWaitStrategyType.BusySpinWaitStrategy => new BusySpinCompletionPollingWaitStrategy(),
                CompletionPollingWaitStrategyType.SpinWaitWaitStrategy => new SpinWaitCompletionPollingWaitStrategy(),
                _                                                      => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
