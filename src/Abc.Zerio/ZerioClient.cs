using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Configuration;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio
{
    public class ZerioClient : IFeedClient
    {
        private readonly IPEndPoint _serverEndpoint;
        private readonly CompletionQueues _completionQueues;
        private readonly ISessionManager _sessionManager;
        private readonly IZerioConfiguration _configuration;
        private readonly Session _session;

        private readonly RequestProcessingEngine _requestProcessingEngine;
        private readonly ReceiveCompletionProcessor _receiveCompletionProcessor;

        private bool _isRunning;
        public  bool IsConnected { get; }
        
        public event Action Connected;
        public event Action Disconnected;
        public event ClientMessageReceivedDelegate MessageReceived;

        public ZerioClient(IPEndPoint serverEndpoint)
        {
            _serverEndpoint = serverEndpoint;

            WinSock.EnsureIsInitialized();

            _configuration = CreateConfiguration();
            _completionQueues = CreateCompletionQueues();
            _sessionManager = CreateSessionManager();

            _requestProcessingEngine = CreateRequestProcessingEngine();
            _receiveCompletionProcessor = CreateReceiveCompletionProcessor();

            _session = _sessionManager.Acquire("serverPeerId");

            _session.Closed += OnSessionClosed;
        }

        private ISessionManager CreateSessionManager()
        {
            return new SessionManager(_configuration, _completionQueues);
        }

        private CompletionQueues CreateCompletionQueues()
        {
            return new CompletionQueues(_configuration);
        }

        private ReceiveCompletionProcessor CreateReceiveCompletionProcessor()
        {
            var receiver = new ReceiveCompletionProcessor(_configuration, _completionQueues.ReceivingQueue, _sessionManager, _requestProcessingEngine);
            receiver.MessageReceived += OnMessageReceived;
            return receiver;
        }

        private static IZerioConfiguration CreateConfiguration()
        {
            return ZerioConfiguration.CreateDefault();
        }

        private RequestProcessingEngine CreateRequestProcessingEngine()
        {
            return new RequestProcessingEngine(_configuration, _completionQueues.SendingQueue, _sessionManager);
        }

        public void Send(ReadOnlySpan<byte> message)
        {
            _requestProcessingEngine.RequestSend(_session.Id, message);
        }

        public Task StartAsync(string peerId)
        {
            if (_isRunning)
                throw new InvalidOperationException("Already started");

            _receiveCompletionProcessor.Start();
            _requestProcessingEngine.Start();

            var socket = CreateSocket();

            _session.Open(socket);

            Connect(socket, _serverEndpoint);

            // todo: send peer Id
            
            _session.InitiateReceiving(_requestProcessingEngine);

            _isRunning = true;

            Connected?.Invoke();
            return Task.CompletedTask;
        }

        private static unsafe void Connect(IntPtr socket, IPEndPoint ipEndPoint)
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

        private static unsafe IntPtr CreateSocket()
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

        private void OnMessageReceived(int sessionId, ArraySegment<byte> message)
        {
            MessageReceived?.Invoke(message);
        }

        private void OnSessionClosed(Session session)
        {
            Disconnected?.Invoke();
        }

        public void Stop()
        {
            _requestProcessingEngine.Stop();
            _receiveCompletionProcessor.Stop();
        }

        public void Dispose()
        {
            Stop();

            _completionQueues?.Dispose();
            _requestProcessingEngine?.Dispose();
            _receiveCompletionProcessor?.Dispose();
            _sessionManager?.Dispose();
        }
    }
}
