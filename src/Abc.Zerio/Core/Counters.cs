namespace Abc.Zerio.Core
{
    // Temporary
    public static class Counters
    {
        public static int FailedSendingNextCount;
        public static int FailedReceivingNextCount;

        public static void Reset()
        {
            FailedReceivingNextCount = FailedReceivingNextCount = 0;
        }
    }
}
