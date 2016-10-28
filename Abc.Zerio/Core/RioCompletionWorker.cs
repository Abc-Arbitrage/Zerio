using System;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    public class RioCompletionWorker : IDisposable
    {
        private Thread _completionWorkerThread;

        public RioCompletionQueue SendingCompletionQueue { get; }

        public RioCompletionQueue ReceivingCompletionQueue => SendingCompletionQueue;

        private readonly int _workerId;
        private readonly IWorkerConfiguration _configuration;
        private readonly ICompletionHandler _completionHandler;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        internal RioCompletionWorker(int workerId, IWorkerConfiguration configuration, ICompletionHandler completionHandler)
        {
            _workerId = workerId;
            _configuration = configuration;
            _completionHandler = completionHandler;
            SendingCompletionQueue = new RioCompletionQueue(configuration.CompletionQueueSize);
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _completionWorkerThread = new Thread(ProcessCompletions);
            _completionWorkerThread.IsBackground = true;
            _completionWorkerThread.Start(SendingCompletionQueue);
        }

        private unsafe void ProcessCompletions(object state)
        {
            Thread.CurrentThread.Name = $"Completion processing Worker #{_workerId}";

            var completionQueue = (RioCompletionQueue)state;
            var maxCompletionResults = _configuration.MaxCompletionResults;
            var results = stackalloc RIO_RESULT[maxCompletionResults];

            int resultCount;
            while ((resultCount = completionQueue.TryGetCompletionResults(_cancellationTokenSource.Token, results, maxCompletionResults)) > 0)
            {
                for (var i = 0; i < resultCount; i++)
                {
                    var result = results[i];
                    var sessionId = (int)result.ConnectionCorrelation;
                    var requestContextKey = new RioRequestContextKey(result.RequestCorrelation);
                    _completionHandler.OnRequestCompletion(sessionId, requestContextKey, (int)result.BytesTransferred);
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _completionWorkerThread.Join(TimeSpan.FromSeconds(10));
        }

        public void Dispose()
        {
            SendingCompletionQueue.Dispose();
        }
    }
}
