using System;
using System.Threading.Tasks;

namespace Abc.Zerio.Core
{
    public interface IFeedClient : IDisposable
    {
        bool IsConnected { get; }

        event Action Connected;
        event Action Disconnected;
        event ClientMessageReceivedDelegate MessageReceived;

        void Send(ReadOnlySpan<byte> message);

        void Start(string peerId);
        void Stop();
    }

    public delegate void ClientMessageReceivedDelegate(ReadOnlySpan<byte> message);
}
