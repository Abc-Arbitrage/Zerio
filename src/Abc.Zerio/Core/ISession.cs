using System;
using Abc.Zerio.Channel;

namespace Abc.Zerio.Core
{
    internal interface ISession : IDisposable
    {
        void Open(IntPtr socket);
        void InitiateReceiving();
        void OnBytesReceived(int bufferSegmentId, int bytesTransferred);
        void RequestReceive(int bufferSegmentId);
        void Close();
        void Reset();
        
        string PeerId { get; }
        int Id { get;  }
        RioRequestQueue RequestQueue { get; }
        event Action<string> HandshakeReceived;
        event Action<ISession> Closed;
        void Send(ReadOnlySpan<byte> messageBytes);
        RegisteredMemoryChannel SendingChannel { get; }
    }
}
