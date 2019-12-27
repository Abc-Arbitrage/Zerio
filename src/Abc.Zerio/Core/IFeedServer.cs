using System;
using System.Threading.Tasks;

namespace Abc.Zerio.Core
{
    public interface IFeedServer : IDisposable
    {
        bool IsRunning { get; }
        
        event Action<string> ClientConnected;
        event Action<string> ClientDisconnected;
        event ServerMessageReceivedDelegate MessageReceived;   
        
       void Send(string peerId, ReadOnlySpan<byte> message);

       Task StartAsync(string peerId);
       void Stop();
    }
    
    public delegate void ServerMessageReceivedDelegate(string peerId, ReadOnlySpan<byte> message);
}
