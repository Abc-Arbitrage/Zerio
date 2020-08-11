namespace Abc.Zerio.Channel
{
    public delegate void ChannelFrameReadDelegate(ChannelFrame frame, bool endOfBatch, CompletionToken token);
}
