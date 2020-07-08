using System;

namespace Abc.Zerio.Channel
{
    public delegate void ChannelMessageReceivedDelegate(ReadOnlySpan<byte> messageByte, bool isEndOfBatch, bool cleanupNeeded);
}
