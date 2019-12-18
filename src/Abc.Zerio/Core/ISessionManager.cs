using System;

namespace Abc.Zerio.Core
{
    internal interface ISessionManager : IDisposable
    {
        bool TryGetRequestQueue(int sessionId, out RioRequestQueue requestQueue);
        bool TryGetSessionByPeerId(string peerId, out Session session);
        
        Session Acquire(string peerId);
        void Release(Session session);
        bool TryGetSession(int sessionId, out Session session);
    }
}
