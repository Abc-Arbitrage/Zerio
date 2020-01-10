namespace Abc.Zerio.Alt.Buffers
{
    public class BufferPoolOptions
    {
        public int SendSegmentCount { get; }
        public int SendSegmentSize { get; }
        public int ReceiveSegmentCount { get; }
        public int ReceiveSegmentSize { get; }
        public bool UseSharedSendPool { get; }

        public BufferPoolOptions(int sendSegmentCount = 2048, int sendSegmentSize = 2048, int receiveSegmentCount = 128, int receiveSegmentSize = 16384, bool useSharedSendPool = true)
        {
            SendSegmentCount = sendSegmentCount;
            SendSegmentSize = sendSegmentSize;
            ReceiveSegmentCount = receiveSegmentCount;
            ReceiveSegmentSize = receiveSegmentSize;
            UseSharedSendPool = useSharedSendPool;
        }
    }
}