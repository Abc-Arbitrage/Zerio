using System;

namespace Abc.Zerio.Core
{
    internal interface ISessionManager : IDisposable
    {
        Session Acquire(string peerId);
        void Release(Session session);
        
        bool TryGetSession(string peerId, out Session session);
        bool TryGetSession(int sessionId, out Session session);
    }
}
