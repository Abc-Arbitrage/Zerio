using System;

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
        SendingRequestConflater Conflater { get; }
        RioRequestQueue RequestQueue { get; }
        SessionSendingBatch SendingBatch { get; }
        
        event Action<string> HandshakeReceived;
        event Action<ISession> Closed;
    }
}
