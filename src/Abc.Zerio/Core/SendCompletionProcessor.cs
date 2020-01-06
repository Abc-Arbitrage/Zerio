using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Interop;
using Disruptor;

namespace Abc.Zerio.Core
{
    internal unsafe class SendCompletionProcessor : IValueEventHandler<RequestEntry>, ILifecycleAware
    {
        private readonly ZerioConfiguration _configuration;
        private readonly RioCompletionQueue _sendCompletionQueue;

        private readonly RIO_RESULT[] _completionResults;
        private readonly RIO_RESULT* _completionResultsPointer;
        private readonly GCHandle _completionResultsHandle;

        private readonly HashSet<long> _releasableSequences = new HashSet<long>();

        public SendCompletionProcessor(ZerioConfiguration configuration, RioCompletionQueue sendCompletionQueue)
        {
            _configuration = configuration;
            _sendCompletionQueue = sendCompletionQueue;
            _completionResults = new RIO_RESULT[configuration.MaxSendCompletionResults];
            _completionResultsHandle = GCHandle.Alloc(_completionResults, GCHandleType.Pinned);
            _completionResultsPointer = (RIO_RESULT*)_completionResultsHandle.AddrOfPinnedObject().ToPointer();
        }

        public void OnEvent(ref RequestEntry data, long sequence, bool endOfBatch)
        {
            if (data.Type == RequestType.Receive)
                return;

            var waitStrategy = CompletionPollingWaitStrategyFactory.Create(_configuration.SendCompletionPollingWaitStrategyType);
            
            while (!_releasableSequences.Remove(sequence))
            {
                var resultCount = _sendCompletionQueue.TryGetCompletionResults(_completionResultsPointer, _completionResults.Length);
                if (resultCount == 0)
                {
                    waitStrategy.Wait();
                    continue;
                }

                waitStrategy.Reset();
                
                for (var i = 0; i < resultCount; i++)
                {
                    var result = _completionResultsPointer[i];
                    var releasableSequence = result.RequestCorrelation;
                    _releasableSequences.Add(releasableSequence);
                }
            }

            data.Reset();
        }

        public void OnStart()
        {
            Thread.CurrentThread.Name = nameof(SendCompletionProcessor);
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
