using System;
using System.Net;
using System.Text;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio
{
    public class ZerioServer : IFeedServer
    {
        private readonly CompletionQueues _completionQueues;
        private readonly InternalZerioConfiguration _configuration;
        private readonly ISessionManager _sessionManager;
        private readonly int _listeningPort;
        private readonly IntPtr _listeningSocket;

        private readonly SendRequestProcessingEngine _sendRequestProcessingEngine;
        private readonly ReceiveCompletionProcessor _receiveCompletionProcessor;

        private bool _isListening;
        private Thread _listeningThread;
        private readonly AutoResetEvent _handshakeSignal = new AutoResetEvent(false);
        private int _started;

        public int ListeningPort { get; set; }

        public ZerioServer(int listeningPort, ZerioServerConfiguration serverConfiguration = null)
        {
            WinSock.EnsureIsInitialized();

            _listeningPort = listeningPort;

            _configuration = CreateConfiguration(serverConfiguration);
            _completionQueues = CreateCompletionQueues();
            _sessionManager = CreateSessionManager();

            _sendRequestProcessingEngine = CreateRequestProcessingEngine();
            _receiveCompletionProcessor = CreateReceiveCompletionProcessor();

            _listeningSocket = CreateListeningSocket();
        }

        private ISessionManager CreateSessionManager()
        {
            var sessionManager = new SessionManager(_configuration, _completionQueues);
            sessionManager.MessageReceived += (peerId, message) => MessageReceived?.Invoke(peerId, message);
            return sessionManager;
        }

        private CompletionQueues CreateCompletionQueues()
        {
            return new CompletionQueues(_configuration);
        }

        private ReceiveCompletionProcessor CreateReceiveCompletionProcessor()
        {
            var receiver = new ReceiveCompletionProcessor(_configuration, _completionQueues.ReceivingQueue, _sessionManager);
            return receiver;
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
            if (!_sessionManager.TryGetSession(peerId, out Session session))
                return;

            _sendRequestProcessingEngine.RequestSend(session.Id, message);
        }

        public void Start(string peerId)
        {
            if(IsRunning)
                throw new InvalidOperationException("Already started");

            CheckOnlyStartedOnce();
            
            _receiveCompletionProcessor.Start();
            _sendRequestProcessingEngine.Start();

            StartListening();

            IsRunning = true;
        }

        public void Stop()
        {
            if(!IsRunning)
                throw new InvalidOperationException("Already stopped");
            
            StopAcceptLoop();

            _sendRequestProcessingEngine.Stop();
            _receiveCompletionProcessor.Stop();

            IsRunning = false;
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
            var clientSession = _sessionManager.Acquire();

            clientSession.Closed += OnClientSessionClosed;

            clientSession.Open(acceptSocket);
            clientSession.HandshakeReceived += OnHandshakeReceived;
            clientSession.InitiateReceiving();

            _handshakeSignal.WaitOne();
            
            ClientConnected?.Invoke(clientSession.PeerId);
        }

        private void OnHandshakeReceived(string peerId)
        {
            var peerIdBytes = Encoding.ASCII.GetBytes(peerId);
            Send(peerId, peerIdBytes.AsSpan());
            _handshakeSignal.Set();
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
            session.Closed -= OnClientSessionClosed;

            var peerId = session.PeerId;

            _sessionManager.Release(session);

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

        private static InternalZerioConfiguration CreateConfiguration(ZerioServerConfiguration serverConfiguration)
        {
            serverConfiguration ??= new ZerioServerConfiguration();
            return serverConfiguration.ToInternalConfiguration();
        }

        private SendRequestProcessingEngine CreateRequestProcessingEngine()
        {
            return new SendRequestProcessingEngine(_configuration, _completionQueues.SendingQueue, _sessionManager);
        }

        public bool IsRunning { get; private set; }
        public event Action<string> ClientConnected;
        public event Action<string> ClientDisconnected;

        private void ReleaseUnmanagedResources()
        {
            if (_listeningSocket != IntPtr.Zero)
                WinSock.closesocket(_listeningSocket);
        }

        private void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();

            if (disposing)
            {
                Stop();

                _completionQueues?.Dispose();
                _sendRequestProcessingEngine?.Dispose();
                _sessionManager?.Dispose();
            }
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
