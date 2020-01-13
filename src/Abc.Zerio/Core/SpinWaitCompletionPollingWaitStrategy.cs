using System.Threading;

namespace Abc.Zerio.Core
{
    internal class SpinWaitCompletionPollingWaitStrategy : ICompletionPollingWaitStrategy
    {
        private SpinWait _spinWait = new SpinWait();

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
