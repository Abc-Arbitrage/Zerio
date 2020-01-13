using System;

namespace Abc.Zerio.Core
{
    public delegate void ClientMessageReceivedDelegate(ReadOnlySpan<byte> message);
}