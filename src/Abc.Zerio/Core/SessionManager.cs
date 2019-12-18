using System;
using System.Collections.Concurrent;
using Abc.Zerio.Configuration;

namespace Abc.Zerio.Core
{
    internal class SessionManager : ISessionManager
    {
        private readonly IZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly ConcurrentStack<Session> _sessions = new ConcurrentStack<Session>();
        private readonly ConcurrentDictionary<int, Session> _activeSessions = new ConcurrentDictionary<int, Session>();
        private readonly ConcurrentDictionary<string, Session> _activeSessionsByPeerId = new ConcurrentDictionary<string, Session>();

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
                var clientSession = new Session(i, _configuration, _completionQueues);
                _sessions.Push(clientSession);
            }
        }

        public Session Acquire(string peerId)
        {
            if (!_sessions.TryPop(out var rioSession))
                throw new InvalidOperationException("No session available");

            rioSession.PeerId = peerId;
            _activeSessionsByPeerId.TryAdd(rioSession.PeerId, rioSession);
            _activeSessions.TryAdd(rioSession.Id, rioSession);
            return rioSession;
        }

        public void Release(Session rioSession)
        {
            _activeSessions.TryRemove(rioSession.Id, out _);
            _activeSessionsByPeerId.TryRemove(rioSession.PeerId, out _);
            rioSession.PeerId = null;
            _sessions.Push(rioSession);
        }

        public bool TryGetSession(int sessionId, out Session rioSession)
        {
            return _activeSessions.TryGetValue(sessionId, out rioSession);
        }

        public bool TryGetSessionByPeerId(string peerId, out Session rioSession)
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
