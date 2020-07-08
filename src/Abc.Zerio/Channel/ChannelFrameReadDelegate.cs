namespace Abc.Zerio.Channel
{
    public delegate void ChannelFrameReadDelegate(ChannelFrame frame, bool isEndOfBatch, bool cleanupNeeded);
}
