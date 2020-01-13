using System;

namespace Abc.Zerio.Core
{
    public interface IFeedServer : IDisposable
    {
        bool IsRunning { get; }
        
        event Action<string> ClientConnected;
        event Action<string> ClientDisconnected;
        event ServerMessageReceivedDelegate MessageReceived;   
        
       void Send(string peerId, ReadOnlySpan<byte> message);

       void Start(string peerId);
       void Stop();
    }
}
