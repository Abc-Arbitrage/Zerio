using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public class ServerConfiguration : RioConfiguration, IServerConfiguration
    {
        public ServerConfiguration(int listeningPort = 15699)
        {
            ListeningPort = listeningPort;
            WorkerCount = 1;
            SessionCount = 5;

            SendingBufferCount *= 10;

            var maxSessionOutstandingRequests = MaxOutstandingSends + MaxOutstandingReceives;
            var sessionPerWorker = SessionCount / WorkerCount;
            CompletionQueueSize = sessionPerWorker * maxSessionOutstandingRequests;
        }

        public int ListeningPort { get; set; }
        public int WorkerCount { get; set; }
        public int SessionCount { get; set; }
    }
}
