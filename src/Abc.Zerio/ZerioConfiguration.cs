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

                SendingBufferLength = 4096,
                ReceivingBufferLength = 4096 * 4,

                MaxSendCompletionResults = 2048,
                MaxReceiveCompletionResults = 2048,

                SessionCount = 1,
            };

            configuration.SendingBufferCount = GetNextPowerOfTwo(100 * oneMegabyte / configuration.SendingBufferLength);
            configuration.ReceivingBufferCount = GetNextPowerOfTwo(100 * oneMegabyte / configuration.ReceivingBufferLength);

            return configuration;
        }

        private static int GetNextPowerOfTwo(int value)
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
