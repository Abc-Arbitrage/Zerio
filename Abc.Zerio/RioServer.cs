using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using Abc.Zerio.Serialization;

namespace Abc.Zerio
{
    public class RioServer : IDisposable, ICompletionHandler
    {
        private readonly IServerConfiguration _configuration;
        private readonly SessionManager _sessionManager;
        private readonly IList<RioCompletionWorker> _workers;

        private readonly IntPtr _listeningSocket;

        private volatile bool _isListening;
        private Thread _listeningThread;

        public event Action<int, object> MessageReceived = delegate { };
        public event Action<int> ClientConnected = delegate { };
        public event Action<int> ClientDisconnected = delegate { };

        public RioServer(IServerConfiguration configuration, SerializationEngine serializationEngine)
        {
            WinSock.EnsureIsInitialized();

            _configuration = configuration;
            _listeningSocket = CreateListeningSocket();
            _workers = CreateWorkers();

            _sessionManager = new SessionManager(configuration);
            _sessionManager.CreateSessions(_workers, serializationEngine);
        }

        public void Start()
        {
            StartWorkers();
            StartListening();
        }

        public void Send(int clientId, object message)
        {
            RioSession session;
            if (!_sessionManager.TryGetSession(clientId, out session))
                return;

            session.EnqueueSend(message);
        }

        private void StartListening()
        {
            _isListening = true;

            _listeningThread = new Thread(Listen);
            _listeningThread.IsBackground = true;
            _listeningThread.Start();
        }

        private void Listen()
        {
            Thread.CurrentThread.Name = "Server Listening Worker";

            if (!Bind(_listeningSocket, _configuration.ListeningPort))
                return;

            if (WinSock.listen(_listeningSocket, WinSock.Consts.SOMAXCONN) == WinSock.Consts.SOCKET_ERROR)
                return;

            while (_isListening)
            {
                var acceptSocket = WinSock.accept(_listeningSocket, IntPtr.Zero, IntPtr.Zero);
                if (acceptSocket == (IntPtr)WinSock.Consts.INVALID_SOCKET)
                {
                    var lastErrorCode = WinSock.WSAGetLastError();
                    if (lastErrorCode == WinSock.Consts.WSAEINTR)
                        break;

                    WinSock.ThrowLastWsaError();
                    continue;
                }

                var clientSession = _sessionManager.Acquire();
                clientSession.Closed += OnClientSessionClosed;
                clientSession.MessageReceived += OnClientSessionMessageReceived;

                clientSession.Open(acceptSocket);
                clientSession.InitiateReceiving();

                ClientConnected(clientSession.Id);
            }
        }

        private void OnClientSessionMessageReceived(RioSession rioSession, object message)
        {
            MessageReceived?.Invoke(rioSession.Id, message);
        }

        private void OnClientSessionClosed(RioSession session)
        {
            session.Closed -= OnClientSessionClosed;
            session.MessageReceived -= OnClientSessionMessageReceived;
            _sessionManager.Release(session);

            ClientDisconnected(session.Id);
        }

        private void StartWorkers()
        {
            foreach (var worker in _workers)
            {
                worker.Start();
            }
        }

        private void StopWorkers()
        {
            foreach (var worker in _workers)
            {
                worker.Stop();
            }
        }

        public void Stop()
        {
            StopAcceptLoop();
            StopWorkers();
        }

        private void StopAcceptLoop()
        {
            _isListening = false;
            WinSock.closesocket(_listeningSocket);
            _listeningThread.Join(TimeSpan.FromSeconds(10));
        }

        private static unsafe IntPtr CreateListeningSocket()
        {
            var socketFlags = SocketFlags.WSA_FLAG_REGISTERED_IO | SocketFlags.WSA_FLAG_OVERLAPPED;
            var listeningSocket = WinSock.WSASocket(AddressFamilies.AF_INET, SocketType.SOCK_STREAM, Protocol.IPPROTO_TCP, IntPtr.Zero, 0, socketFlags);
            if (listeningSocket == (IntPtr)WinSock.Consts.INVALID_SOCKET)
            {
                WinSock.ThrowLastWsaError();
                return IntPtr.Zero;
            }

            var tcpNoDelay = -1;
            WinSock.setsockopt(listeningSocket, WinSock.Consts.IPPROTO_TCP, WinSock.Consts.TCP_NODELAY, (char*)&tcpNoDelay, sizeof(int));

            var reuseAddr = 1;
            WinSock.setsockopt(listeningSocket, WinSock.Consts.SOL_SOCKET, WinSock.Consts.SO_REUSEADDR, (char*)&reuseAddr, sizeof(int));

            return listeningSocket;
        }

        private static unsafe bool Bind(IntPtr listeningSocket, int listeningPort)
        {
            var endPointAddressBytes = IPAddress.Any.GetAddressBytes();
            var inAddress = new InAddr(endPointAddressBytes);

            var sa = new SockaddrIn
            {
                sin_family = AddressFamilies.AF_INET,
                sin_port = WinSock.htons((ushort)listeningPort),
                sin_addr = inAddress
            };

            var errorCode = WinSock.bind(listeningSocket, ref sa, sizeof(SockaddrIn));
            if (errorCode == WinSock.Consts.SOCKET_ERROR)
            {
                WinSock.ThrowLastWsaError();
                return false;
            }

            return true;
        }

        private IList<RioCompletionWorker> CreateWorkers()
        {
            var workers = new List<RioCompletionWorker>();
            for (var i = 0; i < _configuration.WorkerCount; i++)
            {
                workers.Add(new RioCompletionWorker(i, _configuration, this));
            }
            return workers;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~RioServer()
        {
            Dispose(false);
        }

        void ICompletionHandler.OnRequestCompletion(int sessionId, RioRequestContextKey requestContextKey, int bytesTransferred)
        {
            RioSession session;
            if (!_sessionManager.TryGetSession(sessionId, out session))
                return; // disconnected client? it should be fine to ignore and continue polling

            session.OnRequestCompletion(requestContextKey, bytesTransferred);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();

                foreach (var worker in _workers)
                {
                    worker.Dispose();
                }

                _sessionManager?.Dispose();
            }
            if (_listeningSocket != IntPtr.Zero)
                WinSock.closesocket(_listeningSocket);
        }
    }
}
