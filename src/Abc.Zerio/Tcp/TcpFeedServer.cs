using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abc.Zerio.Core;

namespace Abc.Zerio.Tcp
{
    public class TcpFeedServer : IFeedServer
    {
        private readonly Dictionary<string, ClientSession> _clients = new Dictionary<string, ClientSession>();
        private readonly ArrayPool<byte> _arrayPool;
        private readonly int _port;
        private volatile bool _isRunning;
        private Socket _serverSocket;

        private SocketAsyncEventArgs _acceptAsyncEventArgs;

        public TcpFeedServer(int port)
        {
            _port = port;
            _arrayPool = ArrayPool<byte>.Shared;
        }

        public bool IsRunning => _isRunning;
        public event Action<string> ClientConnected = delegate { };
        public event Action<string> ClientDisconnected = delegate { };
        public event ServerMessageReceivedDelegate MessageReceived;

        public Task StartAsync(string peerId)
        {
            if (_isRunning)
                throw new InvalidOperationException("Already started");

            _isRunning = true;

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ExclusiveAddressUse = true,
                NoDelay = true
            };

            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _serverSocket.Listen(16);

            _acceptAsyncEventArgs = new SocketAsyncEventArgs();
            _acceptAsyncEventArgs.Completed += OnAcceptCompleted;

            Accept();

            return Task.CompletedTask;
        }

        private void Accept()
        {
            _acceptAsyncEventArgs.AcceptSocket = null;

            if (!_serverSocket.AcceptAsync(_acceptAsyncEventArgs))
                OnAcceptCompleted(_serverSocket, _acceptAsyncEventArgs);
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                return;

            InitClient(e.AcceptSocket);
            Accept();
        }

        private void InitClient(Socket socket)
        {
            var peerId = Guid.NewGuid().ToString();

            socket.NoDelay = true;

            var client = new ClientSession(socket, _arrayPool);

            lock (_clients)
            {
                _clients.Add(peerId, client);
            }

            ClientConnected?.Invoke(peerId);

            client.MessageReceived += message => OnMessageReceived(peerId, message);
            client.Receive();
        }

        private void OnMessageReceived(string peerId, ReadOnlySpan<byte> message)
        {
            MessageReceived?.Invoke(peerId, message);
        }

        public void Send(string peerId, ReadOnlySpan<byte> message)
        {
            if (message.Length == 0)
                return;

            ClientSession client;

            lock (_clients)
            {
                if (!_clients.TryGetValue(peerId, out client))
                    throw new InvalidOperationException($"Not connected to {peerId}");
            }

            client.Send(message);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            _acceptAsyncEventArgs?.Dispose();
            _acceptAsyncEventArgs = null;

            _serverSocket?.Dispose();
            _serverSocket = null;

            lock (_clients)
            {
                foreach (var (_, client) in _clients)
                    client.Dispose();

                _clients.Clear();
            }
        }

        private class ClientSession : IDisposable
        {
            private readonly Socket _socket;
            private readonly TcpFrameSender _sender;
            private readonly TcpFrameReceiver _receiver;

            public event ClientMessageReceivedDelegate MessageReceived;

            public ClientSession(Socket socket, ArrayPool<byte> arrayPool)
            {
                _socket = socket;

                _sender = new TcpFrameSender(socket, arrayPool);
                _receiver = new TcpFrameReceiver(socket, arrayPool);
                _receiver.MessageReceived += message => MessageReceived?.Invoke(message);
            }

            public void Dispose()
            {
                _sender.Dispose();
                _receiver.Dispose();
                _socket.Dispose();
            }

            public void Receive() => _receiver.Receive();

            public void Send(ReadOnlySpan<byte> message) => _sender.Send(message);
        }

        public void Dispose()
        {
            _serverSocket?.Dispose();
            _acceptAsyncEventArgs?.Dispose();
        }
    }
}
