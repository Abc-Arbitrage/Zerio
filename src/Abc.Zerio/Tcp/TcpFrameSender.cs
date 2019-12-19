using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Abc.Zerio.Tcp
{
    internal class TcpFrameSender : IDisposable
    {
        private readonly Socket _socket;
        private readonly ArrayPool<byte> _arrayPool;

        private readonly SocketAsyncEventArgs _asyncEventArgs = new SocketAsyncEventArgs();

        private readonly Queue<ArraySegment<byte>> _sendQueue = new Queue<ArraySegment<byte>>(1024);
        private byte[] _sendBuffer = new byte[64 * 1024];
        private byte[] _nextBuffer = new byte[64 * 1024];
        private int _nextBufferPos;
        private bool _sending;

        private ArraySegment<byte> _currentMessage;
        private int _currentMessagePos;

        public TcpFrameSender(Socket socket, ArrayPool<byte> arrayPool)
        {
            _socket = socket;
            _arrayPool = arrayPool;

            _socket.NoDelay = true;
            _socket.SendBufferSize = _sendBuffer.Length;

            _asyncEventArgs.Completed += OnSendCompleted;
        }

        public void Dispose()
        {
            _asyncEventArgs.Dispose();
        }

        public void Send(ReadOnlySpan<byte> message)
        {
            if (message.Length == 0)
                return;

            lock (_sendQueue)
            {
                var frameSize = sizeof(int) + message.Length;

                if (_sendQueue.Count == 0 && _nextBufferPos + frameSize < _nextBuffer.Length)
                {
                    Unsafe.WriteUnaligned(ref _nextBuffer[_nextBufferPos], message.Length);
                    message.CopyTo(new Span<byte>(_nextBuffer, _nextBufferPos + sizeof(int), message.Length));
                    _nextBufferPos += frameSize;
                }
                else
                {
                    var buffer = _arrayPool.Rent(message.Length);
                    message.CopyTo(buffer);
                    _sendQueue.Enqueue(new ArraySegment<byte>(buffer, 0, message.Length));
                }

                TrySend();
            }
        }

        private void TrySend()
        {
            if (_sending)
                return;

            while (true)
            {
                var sendBuffer = _sendBuffer;
                var sendPos = 0;

                while (true)
                {
                    if (_currentMessage.Array != null)
                    {
                        var sendableLength = Math.Min(_currentMessage.Count - _currentMessagePos, sendBuffer.Length - sendPos);
                        if (sendableLength != 0)
                        {
                            Unsafe.CopyBlockUnaligned(ref sendBuffer[sendPos], ref _currentMessage.Array[_currentMessage.Offset + _currentMessagePos], (uint)sendableLength);
                            _currentMessagePos += sendableLength;
                            sendPos += sendableLength;
                        }

                        if (_currentMessagePos < _currentMessage.Count)
                            break;

                        _arrayPool.Return(_currentMessage.Array);
                        _currentMessage = default;
                    }

                    if (_nextBufferPos != 0)
                        break;

                    if (_sendQueue.Count == 0)
                        break;

                    if (sendBuffer.Length - sendPos < sizeof(int))
                        break;

                    _currentMessage = _sendQueue.Dequeue();
                    _currentMessagePos = 0;

                    Unsafe.WriteUnaligned(ref sendBuffer[sendPos], _currentMessage.Count);
                    sendPos += sizeof(int);
                }

                if (sendPos == 0)
                {
                    if (_nextBufferPos == 0)
                        return;

                    _sendBuffer = _nextBuffer;
                    _nextBuffer = sendBuffer;
                    sendBuffer = _sendBuffer;

                    sendPos = _nextBufferPos;
                    _nextBufferPos = 0;
                }

                _sending = true;
                _asyncEventArgs.SetBuffer(sendBuffer, 0, sendPos);

                if (SendAsyncSuppressFlow(_socket, _asyncEventArgs))
                    return;

                if (_asyncEventArgs.SocketError != SocketError.Success)
                    return;
                _sending = false;
            }
        }

        private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                return;

            lock (_sendQueue)
            {
                _sending = false;
                TrySend();
            }
        }

        private static bool SendAsyncSuppressFlow(Socket socket, SocketAsyncEventArgs e)
        {
            var control = ExecutionContext.SuppressFlow();
            try
            {
                return socket.SendAsync(e);
            }
            finally
            {
                control.Undo();
            }
        }
    }
}
