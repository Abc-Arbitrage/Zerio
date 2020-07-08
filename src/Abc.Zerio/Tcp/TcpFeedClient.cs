using System;
using System.Net;
using System.Net.Sockets;
using Abc.Zerio.Core;

namespace Abc.Zerio.Tcp
{
    public class TcpFeedClient : IFeedClient
    {
        private readonly IPEndPoint _serverEndpoint;

        private Socket _socket;
        private volatile bool _isRunning;

        private TcpFrameSender _sender;
        private TcpFrameReceiver _receiver;

        public TcpFeedClient(IPEndPoint serverEndpoint)
        {
            _serverEndpoint = serverEndpoint;
        }

        public bool IsConnected => _isRunning;

        public event Action Connected;
        public event Action Disconnected = delegate { };
        public event ClientMessageReceivedDelegate MessageReceived;

        public void Start(string peerId)
        {
            if (_isRunning)
                throw new InvalidOperationException("Already started");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            _socket.Connect(_serverEndpoint);

            _sender = new TcpFrameSender(_socket);
            _receiver = new TcpFrameReceiver(_socket);
            _receiver.MessageReceived += OnMessageReceived;

            _isRunning = true;

            _receiver.StartReceive();

            Connected?.Invoke();
        }

        private void OnMessageReceived(ReadOnlySpan<byte> message)
        {
            MessageReceived?.Invoke(message);
        }

        public void Send(ReadOnlySpan<byte> messageBytes) => _sender.Send(messageBytes);

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            _receiver = null;
            _sender = null;

            _socket.Dispose();
            _socket = null;
        }

        public void Dispose() => Stop();
    }
}
