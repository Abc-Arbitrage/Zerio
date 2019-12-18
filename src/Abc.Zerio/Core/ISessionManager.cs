using System;

namespace Abc.Zerio.Core
{
    internal interface ISessionManager : IDisposable
    {
        bool TryGetRequestQueue(int sessionId, out RioRequestQueue requestQueue);
        bool TryGetSessionByPeerId(string peerId, out ZerioSession session);
        
        ZerioSession Acquire(string peerId);
        void Release(ZerioSession session);
        bool TryGetSession(int sessionId, out ZerioSession session);
    }
}
