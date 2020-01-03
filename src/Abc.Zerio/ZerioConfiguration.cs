namespace Abc.Zerio
{
    public class ZerioConfiguration
    {
        public int SendingBufferCount { get; set; }
        public int SendingBufferLength { get; set; }
        public int ReceivingBufferCount { get; set; }
        public int ReceivingBufferLength { get; set; }
        public int MaxSendBatchSize { get; set; }
        public int MaxSendCompletionResults { get; set; }
        public int MaxReceiveCompletionResults { get; set; }
        public int SessionCount { get; set; }

        public static ZerioConfiguration CreateDefault()
        {
            const int allocationGranularity = 65536;
            const int oneMegabyte = allocationGranularity * 16;

            var configuration = new ZerioConfiguration
            {
                MaxSendBatchSize = 16,

                SendingBufferLength = 1024,
                ReceivingBufferLength = 64 * 1024,

                MaxSendCompletionResults = 64,
                MaxReceiveCompletionResults = 64,

                SessionCount = 1,
            };

            configuration.SendingBufferCount = 10 * oneMegabyte / configuration.SendingBufferLength;
            configuration.ReceivingBufferCount = 10 * oneMegabyte / configuration.ReceivingBufferLength;

            return configuration;
        }

        public static int GetNextPowerOfTwo(int value)
        {
            var powerOfTwo = 2;
            while (powerOfTwo < value)
            {
                powerOfTwo *= 2;
            }

            return powerOfTwo;
        }
    }
}
