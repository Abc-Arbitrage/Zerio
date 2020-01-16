using System;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class ReceiveCompletionProcessor
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly IRioCompletionQueue _receivingCompletionQueue;
        private readonly ISessionManager _sessionManager;

        private bool _isRunning;
        private Thread _completionWorkerThread;

        public ReceiveCompletionProcessor(InternalZerioConfiguration configuration, IRioCompletionQueue receivingCompletionQueue, ISessionManager sessionManager)
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

            var waitStrategy = CompletionPollingWaitStrategyFactory.Create(_configuration.ReceiveCompletionPollingWaitStrategyType);

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
                    var bufferSegmentId = (int)result.RequestCorrelation;

                    OnRequestCompletion(sessionId, bufferSegmentId, (int)result.BytesTransferred);
                }
            }
        }

        private void OnRequestCompletion(int sessionId, int bufferSegmentId, int bytesTransferred)
        {
            if (bytesTransferred == 0)
            {
                Stop();
                return;
            }

            if (!_sessionManager.TryGetSession(sessionId, out var session))
                return;

            try
            {
                session.OnBytesReceived(bufferSegmentId, bytesTransferred);
            }
            finally
            {
                session.RequestReceive(bufferSegmentId);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _completionWorkerThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
