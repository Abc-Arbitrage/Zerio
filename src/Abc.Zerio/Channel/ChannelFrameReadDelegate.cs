namespace Abc.Zerio.Channel
{
    public delegate void ChannelFrameReadDelegate(ChannelFrame frame, bool endOfBatch, SendCompletionToken token);
}
