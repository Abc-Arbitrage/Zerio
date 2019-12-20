using System;
using System.Threading;
using Abc.Zerio.Configuration;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class ReceiveCompletionProcessor : IDisposable
    {
        private readonly IZerioConfiguration _configuration;
        private readonly RioCompletionQueue _receivingCompletionQueue;
        private readonly ISessionManager _sessionManager;
        private readonly RequestProcessingEngine _requestProcessingEngine;

        private Thread _completionWorkerThread;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ReceiveCompletionProcessor(IZerioConfiguration configuration, RioCompletionQueue receivingCompletionQueue, ISessionManager sessionManager, RequestProcessingEngine requestProcessingEngine)
        {
            _configuration = configuration;
            _receivingCompletionQueue = receivingCompletionQueue;
            _sessionManager = sessionManager;
            _requestProcessingEngine = requestProcessingEngine;
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _completionWorkerThread = new Thread(ProcessCompletions) { IsBackground = true };
            _completionWorkerThread.Start(_receivingCompletionQueue);
        }

        private unsafe void ProcessCompletions(object state)
        {
            Thread.CurrentThread.Name = "Receive completion processing thread";

            var completionQueue = (RioCompletionQueue)state;
            var maxCompletionResults = _configuration.MaxReceiveCompletionResults;
            var results = stackalloc RIO_RESULT[maxCompletionResults];

            int resultCount;
            while ((resultCount = completionQueue.TryGetCompletionResults(_cancellationTokenSource.Token, results, maxCompletionResults)) > 0)
            {
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
                _requestProcessingEngine.RequestReceive(session.Id, bufferSegmentId);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _completionWorkerThread.Join(TimeSpan.FromSeconds(10));
        }

        public void Dispose()
        {
            Stop();

            _cancellationTokenSource?.Dispose();
        }
    }
}
