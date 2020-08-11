using System;
using System.Net;
using System.Text;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio
{
    public class ZerioClient : IFeedClient
    {
        private readonly IPEndPoint _serverEndpoint;
        private readonly CompletionQueues _completionQueues;
        private readonly ISessionManager _sessionManager;
        private readonly InternalZerioConfiguration _configuration;
        private readonly ISession _session;

        private readonly SendingProcessor _sendingProcessor;
        private readonly ReceiveCompletionProcessor _receiveCompletionProcessor;
        private readonly SendCompletionProcessor _sendCompletionProcessor;

        private readonly AutoResetEvent _handshakeSignal = new AutoResetEvent(false);
        private IntPtr _socket;
        private int _started;

        public bool IsConnected { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event ClientMessageReceivedDelegate MessageReceived;

        public ZerioClient(IPEndPoint serverEndpoint, ZerioClientConfiguration clientConfiguration = null)
        {
            _serverEndpoint = serverEndpoint;

            WinSock.EnsureIsInitialized();

            _configuration = CreateConfiguration(clientConfiguration);
            _completionQueues = CreateCompletionQueues();
            _sessionManager = CreateSessionManager();

            _sendingProcessor = CreateSendingProcessor();

            _receiveCompletionProcessor = CreateReceiveCompletionProcessor();
            _sendCompletionProcessor = CreateSendCompletionProcessor();

            _session = _sessionManager.Acquire();
            _session.HandshakeReceived += OnHandshakeReceived;
            _session.Closed += OnSessionClosed;
        }

        private SendingProcessor CreateSendingProcessor()
        {
            var sendingProcessor = new SendingProcessor(_configuration, _sessionManager);
            return sendingProcessor;
        }

        private void OnHandshakeReceived(string peerId)
        {
            _handshakeSignal.Set();
        }

        private ISessionManager CreateSessionManager()
        {
            var sessionManager = new SessionManager(_configuration, _completionQueues);
            sessionManager.MessageReceived += (peerId, message) => MessageReceived?.Invoke(message);
            return sessionManager;
        }

        private CompletionQueues CreateCompletionQueues()
        {
            return new CompletionQueues(_configuration);
        }

        private ReceiveCompletionProcessor CreateReceiveCompletionProcessor()
        {
            var receiveCompletionProcessor = new ReceiveCompletionProcessor(_configuration, _completionQueues.ReceivingQueue, _sessionManager);
            return receiveCompletionProcessor;
        }

        private SendCompletionProcessor CreateSendCompletionProcessor()
        {
            var sendCompletionProcessor = new SendCompletionProcessor(_configuration, _completionQueues.SendingQueue, _sessionManager);
            return sendCompletionProcessor;
        }

        private static InternalZerioConfiguration CreateConfiguration(ZerioClientConfiguration clientConfiguration)
        {
            clientConfiguration ??= new ZerioClientConfiguration();
            return clientConfiguration.ToInternalConfiguration();
        }

        public void Send(ReadOnlySpan<byte> messageBytes)
        {
            _session.Send(messageBytes);
        }

        private void CheckOnlyStartedOnce()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException($"{nameof(ZerioClient)} must only be started once.");
        }

        public void Start(string peerId)
        {
            if (IsConnected)
                throw new InvalidOperationException("Already started");

            CheckOnlyStartedOnce();

            _receiveCompletionProcessor.Start();
            _sendCompletionProcessor.Start();
            _sendingProcessor.Start();

            _socket = CreateSocket();

            _session.Open(_socket);

            Connect(_socket, _serverEndpoint);

            _session.InitiateReceiving();

            Handshake(peerId);

            IsConnected = true;
            Connected?.Invoke();
        }

        private void Handshake(string peerId)
        {
            var peerIdBytes = Encoding.ASCII.GetBytes(peerId);
            Send(peerIdBytes.AsSpan());
            _handshakeSignal.WaitOne();
        }

        private unsafe static void Connect(IntPtr socket, IPEndPoint ipEndPoint)
        {
            var endPointAddressBytes = ipEndPoint.Address.GetAddressBytes();
            var inAddress = new InAddr(endPointAddressBytes);

            var sa = new SockaddrIn
            {
                sin_family = AddressFamilies.AF_INET,
                sin_port = WinSock.htons((ushort)ipEndPoint.Port),
                sin_addr = inAddress
            };

            var errorCode = WinSock.connect(socket, ref sa, sizeof(SockaddrIn));
            if (errorCode == WinSock.Consts.SOCKET_ERROR)
                WinSock.ThrowLastWsaError();
        }

        private unsafe static IntPtr CreateSocket()
        {
            var socketFlags = SocketFlags.WSA_FLAG_REGISTERED_IO | SocketFlags.WSA_FLAG_OVERLAPPED;
            var connectionSocket = WinSock.WSASocket(AddressFamilies.AF_INET, SocketType.SOCK_STREAM, Protocol.IPPROTO_TCP, IntPtr.Zero, 0, socketFlags);
            if (connectionSocket == (IntPtr)WinSock.Consts.INVALID_SOCKET)
            {
                WinSock.ThrowLastWsaError();
                return IntPtr.Zero;
            }

            var tcpNoDelay = -1;
            WinSock.setsockopt(connectionSocket, WinSock.Consts.IPPROTO_TCP, WinSock.Consts.TCP_NODELAY, (char*)&tcpNoDelay, sizeof(int));

            var reuseAddr = 1;
            WinSock.setsockopt(connectionSocket, WinSock.Consts.SOL_SOCKET, WinSock.Consts.SO_REUSEADDR, (char*)&reuseAddr, sizeof(int));

            return connectionSocket;
        }

        private void OnSessionClosed(ISession session)
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Stop()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Already stopped");

            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _session.Close();

                _receiveCompletionProcessor.Stop();
                _sendCompletionProcessor.Stop();
                _sendingProcessor.Stop();

                _completionQueues?.Dispose();
                _sessionManager?.Dispose();
                _handshakeSignal?.Dispose();
            }
            else
            {
                if (_socket != IntPtr.Zero)
                    WinSock.closesocket(_socket);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ZerioClient()
        {
            Dispose(false);
        }
    }
}
