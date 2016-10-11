using System;

namespace Abc.Zerio.Server
{
    public class ServerConfiguration : IServerConfiguration
    {
        public int ReceivingBufferCount { get; set; }
        public int SendingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int SendingBufferLength { get; set; }
        public int MaxOutstandingSends { get; set; }
        public int MaxOutstandingReceives { get; set; }
        public int MaxCompletionResults { get; set; }
        public int CompletionQueueSize { get; set; }
        public TimeSpan BufferAcquisitionTimeout { get; set; }

        public int ListeningPort { get; set; }
        public int WorkerCount { get; set; }
        public int SessionCount { get; set; }

        public static readonly ServerConfiguration Default = new ServerConfiguration();

        private ServerConfiguration()
        {
            const int allocationGranularity = 65536;
            const int oneMegabyte = allocationGranularity * 16;

            ListeningPort = 15699;
            WorkerCount = 1;
            SessionCount = 5;

            BufferAcquisitionTimeout = TimeSpan.FromSeconds(60);
            ReceivingBufferLength = 4096 * 2;
            SendingBufferLength = 128;
            ReceivingBufferCount = ComputeBufferCount(50 * oneMegabyte, ReceivingBufferLength);
            SendingBufferCount = ComputeBufferCount(100 * oneMegabyte, SendingBufferLength);
            MaxOutstandingSends = SendingBufferCount;
            MaxOutstandingReceives = ReceivingBufferCount;
            CompletionQueueSize = ComputeDefaultCompletionQueueSize();
            MaxCompletionResults = 2048;
        }

        private int ComputeBufferCount(int sizeInBytes, int bufferLength)
        {
            return sizeInBytes / bufferLength;
        }

        private int ComputeDefaultCompletionQueueSize()
        {
            var maxSessionOutstandingRequests = MaxOutstandingSends + MaxOutstandingReceives;
            var sessionPerWorker = SessionCount / WorkerCount;

            return sessionPerWorker * maxSessionOutstandingRequests;
        }
    }
}
