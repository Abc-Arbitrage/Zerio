using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Abc.Zerio.Tcp
{
    internal class TcpFrameSender
    {
        private readonly Socket _socket;

        public TcpFrameSender(Socket socket)
        {
            _socket = socket;
            _socket.NoDelay = true;
        }

        private readonly byte[] _buffer = new byte[1024 + 512];

        public void Send(ReadOnlySpan<byte> message)
        {
            Unsafe.WriteUnaligned(ref _buffer[0], message.Length);
            message.CopyTo(new Span<byte>(_buffer, sizeof(int), message.Length));

            _socket.Send(new Span<byte>(_buffer, 0, message.Length + sizeof(int)));
        }
    }
}
