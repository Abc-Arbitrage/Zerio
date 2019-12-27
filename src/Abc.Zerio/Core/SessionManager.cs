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

        public event ServerMessageReceivedDelegate MessageReceived;

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
                var session = new Session(i, _configuration, _completionQueues);
                session.MessageReceived += (peerId, message) => MessageReceived?.Invoke(peerId, message); 
                _sessions.Push(session);
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

        public void Release(Session session)
        {
            _activeSessions.TryRemove(session.Id, out _);
            _activeSessionsByPeerId.TryRemove(session.PeerId, out _);

            session.Reset();
            
            _sessions.Push(session);
        }

        public bool TryGetSession(int sessionId, out Session rioSession)
        {
            return _activeSessions.TryGetValue(sessionId, out rioSession);
        }

        public bool TryGetSession(string peerId, out Session rioSession)
        {
            return _activeSessionsByPeerId.TryGetValue(peerId, out rioSession);
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
