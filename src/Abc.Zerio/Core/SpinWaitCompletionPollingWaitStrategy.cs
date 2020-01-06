using System.Threading;

namespace Abc.Zerio.Core
{
    internal struct SpinWaitCompletionPollingWaitStrategy : ICompletionPollingWaitStrategy
    {
        private SpinWait _spinWait;

        public SpinWaitCompletionPollingWaitStrategy(SpinWait spinWait)
        {
            _spinWait = spinWait;
        }

        public void Wait()
        {
            _spinWait.SpinOnce();
        }

        public void Reset()
        {
            _spinWait.Reset();
        }
    }
}
