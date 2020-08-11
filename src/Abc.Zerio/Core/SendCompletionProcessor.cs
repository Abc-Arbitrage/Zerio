using System;
using System.Threading;
using Abc.Zerio.Channel;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class SendCompletionProcessor
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly IRioCompletionQueue _receivingCompletionQueue;
        private readonly ISessionManager _sessionManager;

        private bool _isRunning;
        private Thread _completionWorkerThread;

        public SendCompletionProcessor(InternalZerioConfiguration configuration, IRioCompletionQueue receivingCompletionQueue, ISessionManager sessionManager)
        {
            _configuration = configuration;
            _receivingCompletionQueue = receivingCompletionQueue;
            _sessionManager = sessionManager;
        }

        public void Start()
        {
            _isRunning = true;
            _completionWorkerThread = new Thread(ProcessCompletions) { IsBackground = true };
            _completionWorkerThread.Start(_receivingCompletionQueue);
        }

        private unsafe void ProcessCompletions(object state)
        {
            Thread.CurrentThread.Name = nameof(ReceiveCompletionProcessor);

            var completionQueue = (IRioCompletionQueue)state;
            var maxCompletionResults = _configuration.MaxReceiveCompletionResults;
            var results = stackalloc RIO_RESULT[maxCompletionResults];

            var waitStrategy = CompletionPollingWaitStrategyFactory.Create(_configuration.SendCompletionPollingWaitStrategyType);

            while (_isRunning)
            {
                var resultCount = completionQueue.TryGetCompletionResults(results, maxCompletionResults);
                if (resultCount == 0)
                {
                    waitStrategy.Wait();
                    continue;
                }

                waitStrategy.Reset();

                for (var i = 0; i < resultCount; i++)
                {
                    var result = results[i];
                    var sessionId = (int)result.ConnectionCorrelation;
                    
                    if (!_sessionManager.TryGetSession(sessionId, out var session))
                        return;
                    
                    var sendCompletionToken = (SendCompletionToken)result.RequestCorrelation;

                    if(sendCompletionToken.IsEndOfBatch)
                        session.SendingChannel.CompleteSend(sendCompletionToken);
           
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _completionWorkerThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
