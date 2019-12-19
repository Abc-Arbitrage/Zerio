using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Abc.Zerio.Core;

namespace Abc.Zerio.Tcp
{
    public class TcpFeedClient : IFeedClient
    {
        private readonly IPEndPoint _serverEndpoint;
        private readonly ArrayPool<byte> _arrayPool;

        private Socket _socket;
        private volatile bool _isRunning;

        private TcpFrameSender _sender;
        private TcpFrameReceiver _receiver;

        public TcpFeedClient(IPEndPoint serverEndpoint)
        {
            _arrayPool = ArrayPool<byte>.Shared;
            _serverEndpoint = serverEndpoint;
        }

        public bool IsConnected => _isRunning;

        public event Action Connected;
        public event Action Disconnected = delegate {};
        public event ClientMessageReceivedDelegate MessageReceived;

        public async Task StartAsync(string peerId)
        {
            if (_isRunning)
                throw new InvalidOperationException("Already started");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            await _socket.ConnectAsync(_serverEndpoint);

            _sender = new TcpFrameSender(_socket, _arrayPool);
            _receiver = new TcpFrameReceiver(_socket, _arrayPool);
            _receiver.MessageReceived += OnMessageReceived;

            _isRunning = true;

            _receiver.Receive();

            Connected?.Invoke();
        }

        private void OnMessageReceived(ReadOnlySpan<byte> message)
        {
            MessageReceived?.Invoke(message);
        }

        public void Send(ReadOnlySpan<byte> message) => _sender.Send(message);

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            _receiver.Dispose();
            _receiver = null;

            _sender.Dispose();
            _sender = null;

            _socket.Dispose();
            _socket = null;
        }

        public void Dispose() => Stop();
    }
}
