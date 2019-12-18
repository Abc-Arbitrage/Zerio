using System;
using System.Collections.Concurrent;
using Abc.Zerio.Configuration;

namespace Abc.Zerio.Core
{
    internal class SessionManager : ISessionManager
    {
        private readonly IZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly ConcurrentStack<ZerioSession> _sessions = new ConcurrentStack<ZerioSession>();
        private readonly ConcurrentDictionary<int, ZerioSession> _activeSessions = new ConcurrentDictionary<int, ZerioSession>();
        private readonly ConcurrentDictionary<string, ZerioSession> _activeSessionsByPeerId = new ConcurrentDictionary<string, ZerioSession>();

        private int _sessionCount;

        public SessionManager(IZerioConfiguration configuration, CompletionQueues completionQueues)
        {
            _configuration = configuration;
            _completionQueues = completionQueues;

            CreateSessions();
        }

        private void CreateSessions()
        {
            if (_configuration.SessionCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(_configuration.SessionCount));

            _sessions.Clear();
            _sessionCount = _configuration.SessionCount;

            for (var i = 0; i < _sessionCount; i++)
            {
                var clientSession = new ZerioSession(i, _configuration, _completionQueues);
                _sessions.Push(clientSession);
            }
        }

        public ZerioSession Acquire(string peerId)
        {
            if (!_sessions.TryPop(out var rioSession))
                throw new InvalidOperationException("No session available");

            rioSession.PeerId = peerId;
            _activeSessionsByPeerId.TryAdd(rioSession.PeerId, rioSession);
            _activeSessions.TryAdd(rioSession.Id, rioSession);
            return rioSession;
        }

        public void Release(ZerioSession rioSession)
        {
            _activeSessions.TryRemove(rioSession.Id, out _);
            _activeSessionsByPeerId.TryRemove(rioSession.PeerId, out _);
            rioSession.PeerId = null;
            _sessions.Push(rioSession);
        }

        public bool TryGetSession(int sessionId, out ZerioSession rioSession)
        {
            return _activeSessions.TryGetValue(sessionId, out rioSession);
        }

        public bool TryGetSessionByPeerId(string peerId, out ZerioSession rioSession)
        {
            return _activeSessionsByPeerId.TryGetValue(peerId, out rioSession);
        }

        public bool TryGetRequestQueue(int sessionId, out RioRequestQueue requestQueue)
        {
            requestQueue = default;

            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            requestQueue = session.RequestQueue;
            return true;
        }

        public void Dispose()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
        }
    }
}
