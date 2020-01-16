using System;
using System.Collections.Generic;

namespace Abc.Zerio.Core
{
    internal interface ISessionManager : IDisposable
    {
        ISession Acquire();
        void Release(ISession session);
        
        bool TryGetSession(string peerId, out ISession session);
        bool TryGetSession(int sessionId, out ISession session);
        
        IEnumerable<ISession> Sessions { get; }
    }
}
