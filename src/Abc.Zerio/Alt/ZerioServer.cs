using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using JetBrains.Annotations;

namespace Abc.Zerio.Alt
{
    public class ZerioServer : IFeedServer
    {
        private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();
        private readonly int _listeningPort;
        private readonly IntPtr _listeningSocket;

        private bool _isListening;
        private Thread _listeningThread;
        private int _started;
        private RioBufferPools _pools;
        private CancellationTokenSource _cts;
        private Poller _poller;

        [UsedImplicitly]
        public int ListeningPort { get; set; }

        public ZerioServer(int listeningPort)
        {
            WinSock.EnsureIsInitialized();
            _listeningPort = listeningPort;
            _listeningSocket = CreateListeningSocket();
            _cts = new CancellationTokenSource();
            _pools = new RioBufferPools(ct: _cts.Token);
            _poller = new Poller("server_poller", _cts.Token);
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

        private void StartListening()
        {
            _isListening = true;

            var listeningSignal = new ManualResetEventSlim();

            _listeningThread = new Thread(() => ListenAndRunAcceptLoop(listeningSignal)) { IsBackground = true };
            _listeningThread.Start();

            listeningSignal.Wait(2000);
        }

        public event ServerMessageReceivedDelegate MessageReceived;

        public void Send(string peerId, ReadOnlySpan<byte> message)
        {
            if (!_sessions.TryGetValue(peerId, out Session session))
                return;

            session.Send(message);
        }

        public void Start(string peerId)
        {
            if (IsRunning)
                throw new InvalidOperationException("Already started");

            CheckOnlyStartedOnce();
            StartListening();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
                throw new InvalidOperationException("Already stopped");

            StopAcceptLoop();

            IsRunning = false;
            _cts.Cancel();
        }

        private void CheckOnlyStartedOnce()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException($"{nameof(ZerioClient)} must only be started once.");
        }

        private void ListenAndRunAcceptLoop(ManualResetEventSlim listeningSignal)
        {
            Thread.CurrentThread.Name = "Server Listening Worker";

            if (!Bind(_listeningSocket, _listeningPort))
                return;

            ListeningPort = GetSocketPort();

            Listen();

            listeningSignal.Set();

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

                InitClientSession(acceptSocket);
            }
        }

        private void InitClientSession(IntPtr acceptSocket)
        {
            var clientSession = new Session(0, acceptSocket, _pools, _poller, OnMessageReceived, OnClientSessionClosed);
            clientSession.HandshakeSignal.WaitOne(); // TODO timeout
            _sessions.TryAdd(clientSession.PeerId, clientSession);
            ClientConnected?.Invoke(clientSession.PeerId);
        }

        private void OnMessageReceived(string peerId, ReadOnlySpan<byte> message)
        {
            MessageReceived?.Invoke(peerId, message);
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

        private void OnClientSessionClosed(Session session)
        {
            var peerId = session.PeerId;
            _sessions.TryRemove(peerId, out _);
            ClientDisconnected?.Invoke(peerId);
        }

        private void Listen()
        {
            if (WinSock.listen(_listeningSocket, WinSock.Consts.SOMAXCONN) != 0)
                WinSock.ThrowLastWsaError();
        }

        private unsafe int GetSocketPort()
        {
            SockaddrIn sockaddr;
            var sockaddrSize = sizeof(SockaddrIn);

            if (WinSock.getsockname(_listeningSocket, (byte*)&sockaddr, ref sockaddrSize) != 0)
                WinSock.ThrowLastWsaError();

            return WinSock.ntohs(sockaddr.sin_port);
        }

        private void StopAcceptLoop()
        {
            _isListening = false;
            WinSock.closesocket(_listeningSocket);
            _listeningThread.Join(TimeSpan.FromSeconds(10));
        }

        public bool IsRunning { get; private set; }
        public event Action<string> ClientConnected;
        public event Action<string> ClientDisconnected;

        private void ReleaseUnmanagedResources()
        {
            if (_listeningSocket != IntPtr.Zero)
                WinSock.closesocket(_listeningSocket);

            WinSock.WSACleanup();
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
            }

            ReleaseUnmanagedResources();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ZerioServer()
        {
            Dispose(false);
        }
    }
}
