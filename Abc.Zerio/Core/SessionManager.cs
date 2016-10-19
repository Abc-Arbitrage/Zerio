using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Abc.Zerio.Serialization;

namespace Abc.Zerio.Core
{
    public class SessionManager
    {
        private readonly IServerConfiguration _configuration;
        private readonly ConcurrentStack<RioSession> _sessions = new ConcurrentStack<RioSession>();
        private readonly ConcurrentDictionary<int, RioSession> _activeSessions = new ConcurrentDictionary<int, RioSession>();

        private int _sessionCount;

        public SessionManager(IServerConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void CreateSessions(IList<RioCompletionWorker> workers, SerializationEngine serializationEngine)
        {
            if (_configuration.SessionCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(_configuration.SessionCount));

            _sessions.Clear();
            _sessionCount = _configuration.SessionCount;

            for (var i = 0; i < _sessionCount; i++)
            {
                var worker = workers[i % workers.Count];
                AddNewSession(i, worker, serializationEngine);
            }
        }

        private void AddNewSession(int sessionId, RioCompletionWorker completionWorker, SerializationEngine serializationEngine)
        {
            var clientSession = new RioSession(sessionId, _configuration, completionWorker.SendingCompletionQueue, completionWorker.ReceivingCompletionQueue, serializationEngine);
            _sessions.Push(clientSession);
        }

        public RioSession Acquire()
        {
            RioSession rioSession;
            if (_sessions.TryPop(out rioSession))
            {
                _activeSessions.TryAdd(rioSession.Id, rioSession);
                return rioSession;
            }

            throw new InvalidOperationException("No session available");
        }

        public void Release(RioSession rioSession)
        {
            RioSession unused;
            _activeSessions.TryRemove(rioSession.Id, out unused);
            _sessions.Push(rioSession);
        }

        public bool TryGetSession(int sessionId, out RioSession rioSession)
        {
            return _activeSessions.TryGetValue(sessionId, out rioSession);
        }

        public void Dispose()
        {
            foreach (var clientSession in _sessions)
            {
                clientSession.Dispose();
            }
        }
    }
}
