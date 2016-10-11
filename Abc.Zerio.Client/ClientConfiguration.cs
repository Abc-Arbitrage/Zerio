using System;

namespace Abc.Zerio.Client
{
    public class ClientConfiguration : IClientConfiguration
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

        public static readonly ClientConfiguration Default = new ClientConfiguration();

        private ClientConfiguration()
        {
            const int allocationGranularity = 65536;
            const int oneMegabyte = allocationGranularity * 16;

            BufferAcquisitionTimeout = TimeSpan.FromSeconds(60);
            ReceivingBufferLength = 4096 * 2;
            SendingBufferLength = 128;
            ReceivingBufferCount = ComputeBufferCount(50 * oneMegabyte, ReceivingBufferLength);
            SendingBufferCount = ComputeBufferCount(10 * oneMegabyte, SendingBufferLength);
            MaxOutstandingSends = SendingBufferCount;
            MaxOutstandingReceives = ReceivingBufferCount;
            CompletionQueueSize = MaxOutstandingSends + MaxOutstandingReceives;
            MaxCompletionResults = 2048;
        }

        private int ComputeBufferCount(int sizeInBytes, int bufferLength)
        {
            return sizeInBytes / bufferLength;
        }
    }
}
