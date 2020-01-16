using System;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Interop;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class SendCompletionProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware, IEventProcessorSequenceAware
    {
        private readonly CompletionTracker _completionTracker = new CompletionTracker();
        private readonly IRioCompletionQueue _sendCompletionQueue;
        private readonly ICompletionPollingWaitStrategy _waitStrategy;

        private readonly RIO_RESULT[] _completionResults;
        private readonly RIO_RESULT* _completionResultsPointer;
        private GCHandle _completionResultsHandle;

        private ISequence _entryReleasingSequence;

        public SendCompletionProcessor(InternalZerioConfiguration configuration, IRioCompletionQueue sendCompletionQueue)
        {
            _sendCompletionQueue = sendCompletionQueue;
            _completionResults = new RIO_RESULT[configuration.MaxSendCompletionResults];
            _completionResultsHandle = GCHandle.Alloc(_completionResults, GCHandleType.Pinned);
            _completionResultsPointer = (RIO_RESULT*)_completionResultsHandle.AddrOfPinnedObject().ToPointer();

            _waitStrategy = CompletionPollingWaitStrategyFactory.Create(configuration.SendCompletionPollingWaitStrategyType);
        }

        public void OnEvent(ref RequestEntry entry, long sequence, bool endOfBatch)
        {
            try
            {
                if (entry.Type != RequestType.Send)
                {
                    _completionTracker.MarketAsCompleted(sequence);
                    return;
                }

                _waitStrategy.Reset();

                while (!_completionTracker.IsCompleted(sequence))
                {
                    var resultCount = _sendCompletionQueue.TryGetCompletionResults(_completionResultsPointer, _completionResults.Length);
                    if (resultCount == 0)
                    {
                        _waitStrategy.Wait();
                        continue;
                    }

                    _waitStrategy.Reset();

                    for (var i = 0; i < resultCount; i++)
                    {
                        var result = _completionResultsPointer[i];
                        _completionTracker.MarketAsCompleted(result.RequestCorrelation);
                    }
                }
            }
            finally
            {
                entry.Reset();
                
                _entryReleasingSequence.SetValue(sequence);
            }
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(SendCompletionProcessor);
        }

        public void SetSequenceCallback(ISequence entryReleasingSequence)
        {
            _entryReleasingSequence = entryReleasingSequence;
        }

        public void OnShutdown()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _completionResultsHandle.Free();

            if (disposing)
                _sendCompletionQueue?.Dispose();
        }

        ~SendCompletionProcessor()
        {
            Dispose(false);
        }
    }
}
