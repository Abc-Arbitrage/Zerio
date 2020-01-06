using Disruptor;

namespace Abc.Zerio.Core
{
    public class HybridWaitStrategy : INonBlockingWaitStrategy
    {
        private readonly BusySpinWaitStrategy _busySpinWaitStrategy = new BusySpinWaitStrategy();

        private readonly SpinWaitWaitStrategy _spinWaitWaitStrategy = new SpinWaitWaitStrategy();

        public ISequenceBarrier SequenceBarrierForSendCompletionProcessor { get; set; }

        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, ISequenceBarrier barrier)
        {
            if (barrier == SequenceBarrierForSendCompletionProcessor)
                return _spinWaitWaitStrategy.WaitFor(sequence, cursor, dependentSequence, barrier);

            return _busySpinWaitStrategy.WaitFor(sequence, cursor, dependentSequence, barrier);
        }

        public void SignalAllWhenBlocking()
        {
        }
    }
}
