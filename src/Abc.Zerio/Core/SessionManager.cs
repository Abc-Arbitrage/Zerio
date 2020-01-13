using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zerio.Core
{
    internal class SessionManager : ISessionManager
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly ConcurrentStack<Session> _sessions = new ConcurrentStack<Session>();
        private readonly ConcurrentDictionary<int, Session> _activeSessions = new ConcurrentDictionary<int, Session>();
        private readonly ConcurrentDictionary<string, Session> _activeSessionsByPeerId = new ConcurrentDictionary<string, Session>();

        public event ServerMessageReceivedDelegate MessageReceived;

        public SessionManager(InternalZerioConfiguration configuration, CompletionQueues completionQueues)
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

            for (var i = 0; i < _configuration.SessionCount; i++)
            {
                var session = new Session(i, _configuration, _completionQueues);
                session.MessageReceived += (peerId, message) => MessageReceived?.Invoke(peerId, message);
                session.HandshakeReceived += peerId => OnHandshakeReceived(peerId, session);
                _sessions.Push(session);
            }
        }

        private void OnHandshakeReceived(string peerId, Session session)
        {
            _activeSessionsByPeerId.TryAdd(peerId, session);
        }

        public Session Acquire()
        {
            if (!_sessions.TryPop(out var rioSession))
                throw new InvalidOperationException("No session available");

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

        public IEnumerable<Session> Sessions => _sessions;

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
