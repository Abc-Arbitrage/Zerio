using System;

namespace Abc.Zerio.Core
{
    public delegate void ServerMessageReceivedDelegate(string peerId, ReadOnlySpan<byte> message);
}