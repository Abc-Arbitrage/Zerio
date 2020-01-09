using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;

namespace Abc.Zerio.Core
{
    internal class SendRequestProcessingEngine : IDisposable
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly UnmanagedRioBuffer<RequestEntry> _unmanagedRioBuffer;

        private readonly UnmanagedRingBuffer<RequestEntry> _ringBuffer;
        private readonly UnmanagedDisruptor<RequestEntry> _disruptor;

        public SendRequestProcessingEngine(InternalZerioConfiguration configuration, RioCompletionQueue sendingCompletionQueue, ISessionManager sessionManager)
        {
            _configuration = configuration;

            var ringBufferSize = configuration.SendRequestProcessingEngineRingBufferSize;
            _unmanagedRioBuffer = new UnmanagedRioBuffer<RequestEntry>(ringBufferSize, _configuration.SendingBufferLength);

            _disruptor = CreateDisruptor(sendingCompletionQueue, sessionManager);
            _ringBuffer = _disruptor.RingBuffer;
        }

        private unsafe UnmanagedDisruptor<RequestEntry> CreateDisruptor(RioCompletionQueue sendingCompletionQueue, ISessionManager sessionManager)
        {
            var waitStrategy = CreateWaitStrategy();

            var disruptor = new UnmanagedDisruptor<RequestEntry>((IntPtr)_unmanagedRioBuffer.FirstEntry,
                                                                 _unmanagedRioBuffer.EntryReservedSpaceSize,
                                                                 _unmanagedRioBuffer.Length,
                                                                 new ThreadPerTaskScheduler(),
                                                                 ProducerType.Multi,
                                                                 waitStrategy);

            var sendRequestProcessor = CreateSendRequestProcessor(sessionManager);
            var sendCompletionProcessor = new SendCompletionProcessor(_configuration, sendingCompletionQueue);

            disruptor.HandleEventsWith(sendRequestProcessor).Then(sendCompletionProcessor);

            ConfigureWaitStrategy(waitStrategy, disruptor, sendCompletionProcessor);

            return disruptor;
        }

        private IValueEventHandler<RequestEntry> CreateSendRequestProcessor(ISessionManager sessionManager)
        {
            if (_configuration.BatchSendRequests)
                return new BatchingSendRequestProcessor(_configuration, sessionManager);

            return new SendRequestProcessor(sessionManager);
        }

        private static void ConfigureWaitStrategy(IWaitStrategy waitStrategy, UnmanagedDisruptor<RequestEntry> disruptor, SendCompletionProcessor sendCompletionProcessor)
        {
            switch (waitStrategy)
            {
                case HybridWaitStrategy hybridWaitStrategy:
                    hybridWaitStrategy.SequenceBarrierForSendCompletionProcessor = disruptor.GetBarrierFor(sendCompletionProcessor);
                    return;
            }
        }

        private IWaitStrategy CreateWaitStrategy()
        {
            return _configuration.RequestEngineWaitStrategyType switch
            {
                RequestEngineWaitStrategyType.HybridWaitStrategy   => (IWaitStrategy)new HybridWaitStrategy(),
                RequestEngineWaitStrategyType.BlockingWaitStrategy => new BlockingWaitStrategy(),
                RequestEngineWaitStrategyType.SleepingWaitStrategy => new SleepingWaitStrategy(),
                RequestEngineWaitStrategyType.YieldingWaitStrategy => new YieldingWaitStrategy(),
                RequestEngineWaitStrategyType.BusySpinWaitStrategy => new BusySpinWaitStrategy(),
                RequestEngineWaitStrategyType.SpinWaitWaitStrategy => new SpinWaitWaitStrategy(),
                _                                                  => throw new ArgumentOutOfRangeException()
            };
        }

        public void RequestSend(int sessionId, ReadOnlySpan<byte> message)
        {
            var sequence = GetSendingNextSequence();
            try
            {
                ref var sendingEntry = ref _ringBuffer[sequence];
                sendingEntry.SetWriteRequest(sessionId, message);
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }

        private long GetSendingNextSequence()
        {
            long sequence;
            while (!_ringBuffer.TryNext(out sequence))
                Counters.FailedSendingNextCount++;

            return sequence;
        }

        public void Start()
        {
            _disruptor.Start();
        }

        public void Stop()
        {
            _disruptor.Shutdown();
        }

        public void Dispose()
        {
            Stop();

            _unmanagedRioBuffer?.Dispose();
        }

        private class ThreadPerTaskScheduler : TaskScheduler
        {
            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return Enumerable.Empty<Task>();
            }

            protected override void QueueTask(Task task)
            {
                new Thread(() => TryExecuteTask(task)) { IsBackground = true }.Start();
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return TryExecuteTask(task);
            }
        }
    }
}
