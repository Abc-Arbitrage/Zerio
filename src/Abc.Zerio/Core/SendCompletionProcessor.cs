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
        
        private readonly ICompletionPollingWaitStrategy _waitStrategy;

        public SendCompletionProcessor(ZerioConfiguration configuration, RioCompletionQueue sendCompletionQueue)
        {
            _configuration = configuration;
            _sendCompletionQueue = sendCompletionQueue;
            _completionResults = new RIO_RESULT[configuration.MaxSendCompletionResults];
            _completionResultsHandle = GCHandle.Alloc(_completionResults, GCHandleType.Pinned);
            _completionResultsPointer = (RIO_RESULT*)_completionResultsHandle.AddrOfPinnedObject().ToPointer();
            
            _waitStrategy = CompletionPollingWaitStrategyFactory.Create(_configuration.SendCompletionPollingWaitStrategyType);
        }

        public void OnEvent(ref RequestEntry entry, long sequence, bool endOfBatch)
        {
            try
            {
                if (entry.Type != RequestType.Send)
                    return;
            
                _waitStrategy.Reset();
            
                while (!_releasableSequences.Remove(sequence))
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
                        _releasableSequences.Add(result.RequestCorrelation);
                    }
                }   
            }
            finally
            {
                entry.Reset();
            }
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
