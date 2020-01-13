using System;
using System.Collections.Generic;

namespace Abc.Zerio.Core
{
    internal interface ISessionManager : IDisposable
    {
        Session Acquire();
        void Release(Session session);
        
        bool TryGetSession(string peerId, out Session session);
        bool TryGetSession(int sessionId, out Session session);
        
        IEnumerable<Session> Sessions { get; }
    }
}
