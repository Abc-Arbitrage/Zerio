using System;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Abc.Zerio.Core;

namespace Abc.Zerio.Tcp
{
    internal class TcpFrameReceiver : IDisposable
    {
        private readonly Socket _socket;
        private readonly ArrayPool<byte> _arrayPool;

        private readonly SocketAsyncEventArgs _asyncEventArgs = new SocketAsyncEventArgs();
        private readonly byte[] _buffer = new byte[64 * 1024];
        private readonly byte[] _headerBuffer = new byte[sizeof(int)];
        private byte[] _message;
        private int _messageStartOffset, _messagePos, _messageSize;

        public event ClientMessageReceivedDelegate MessageReceived;

        public TcpFrameReceiver(Socket socket, ArrayPool<byte> arrayPool)
        {
            _socket = socket;
            _arrayPool = arrayPool;

            _socket.ReceiveBufferSize = _buffer.Length;

            _asyncEventArgs.Completed += OnReceiveCompleted;

            _message = _headerBuffer;
            _messageSize = _headerBuffer.Length;
            _messagePos = 0;
        }

        public void Dispose()
        {
            _asyncEventArgs.Dispose();
        }

        public void Receive()
            => Receive(0);

        private void Receive(int startOffset)
        {
            while (true)
            {
                _asyncEventArgs.SetBuffer(_buffer, startOffset, _buffer.Length - startOffset);

                if (ReceiveAsyncSuppressFlow(_socket, _asyncEventArgs))
                    return;

                startOffset = HandleReceiveCompleted(_asyncEventArgs);
                if (startOffset < 0)
                    return;
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            var nextReceiveOffset = HandleReceiveCompleted(e);
            if (nextReceiveOffset >= 0)
                Receive(nextReceiveOffset);
        }

        private int HandleReceiveCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                return -1; // yolo

            var buffer = e.Buffer;
            var pos = 0;
            var size = e.BytesTransferred;

            while (true)
            {
                if (_message == _headerBuffer)
                {
                    for (var headerBytes = Math.Min(_messageSize - _messagePos, size - pos); headerBytes > 0; --headerBytes)
                    {
                        _message[_messagePos++] = buffer[e.Offset + pos++];
                    }

                    if (_messagePos < _messageSize)
                        return 0;

                    _messageSize = Unsafe.ReadUnaligned<int>(ref _message[0]);

                    if (buffer.Length - e.Offset - pos >= _messageSize)
                    {
                        _message = _buffer;
                        _messageStartOffset = e.Offset + pos;
                    }
                    else
                    {
                        _message = _arrayPool.Rent(_messageSize);
                        _messageStartOffset = 0;
                    }

                    _messagePos = 0;
                }

                var messageBytes = Math.Min(_messageSize - _messagePos, size - pos);

                if (_message == _buffer)
                {
                    _messagePos += messageBytes;
                    pos += messageBytes;

                    if (_messagePos < _messageSize)
                        return e.Offset + e.BytesTransferred;

                    MessageReceived?.Invoke(new ReadOnlySpan<byte>(_message, _messageStartOffset, _messageSize));
                }
                else
                {
                    if (messageBytes != 0)
                    {
                        Unsafe.CopyBlockUnaligned(ref _message[_messagePos], ref buffer[e.Offset + pos], (uint)messageBytes);
                        pos += messageBytes;
                        _messagePos += messageBytes;
                    }

                    if (_messagePos < _messageSize)
                        return 0;

                    MessageReceived?.Invoke(new ReadOnlySpan<byte>(_message, _messageStartOffset, _messageSize));

                    _arrayPool.Return(_message);
                }

                _message = _headerBuffer;
                _messageSize = _headerBuffer.Length;
                _messagePos = 0;
            }
        }

        private static bool ReceiveAsyncSuppressFlow(Socket socket, SocketAsyncEventArgs e)
        {
            var control = ExecutionContext.SuppressFlow();
            try
            {
                return socket.ReceiveAsync(e);
            }
            finally
            {
                control.Undo();
            }
        }
    }
}
