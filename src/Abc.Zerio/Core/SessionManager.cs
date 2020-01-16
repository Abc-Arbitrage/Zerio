using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zerio.Core
{
    internal class SessionManager : ISessionManager
    {
        private readonly InternalZerioConfiguration _configuration;
        private readonly CompletionQueues _completionQueues;
        private readonly ConcurrentStack<ISession> _sessions = new ConcurrentStack<ISession>();
        private readonly ConcurrentDictionary<int, ISession> _activeSessions = new ConcurrentDictionary<int, ISession>();
        private readonly ConcurrentDictionary<string, ISession> _activeSessionsByPeerId = new ConcurrentDictionary<string, ISession>();

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

        public ISession Acquire()
        {
            if (!_sessions.TryPop(out var rioSession))
                throw new InvalidOperationException("No session available");

            _activeSessions.TryAdd(rioSession.Id, rioSession);
            return rioSession;
        }

        public void Release(ISession session)
        {
            _activeSessions.TryRemove(session.Id, out _);
            _activeSessionsByPeerId.TryRemove(session.PeerId, out _);

            session.Reset();
            
            _sessions.Push(session);
        }

        public bool TryGetSession(int sessionId, out ISession rioSession)
        {
            return _activeSessions.TryGetValue(sessionId, out rioSession);
        }

        public IEnumerable<ISession> Sessions => _sessions;

        public bool TryGetSession(string peerId, out ISession rioSession)
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
