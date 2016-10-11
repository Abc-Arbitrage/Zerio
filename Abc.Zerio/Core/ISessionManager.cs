using System;
using System.Collections.Generic;
using Abc.Zerio.Serialization;

namespace Abc.Zerio.Core
{
    public interface ISessionManager : IDisposable
    {
        void CreateSessions(IList<RioCompletionWorker> workers, SerializationEngine serializationEngine);

        RioSession Acquire();
        void Release(RioSession rioSession);

        bool TryGetSession(int sessionId, out RioSession rioSession);
    }
}
