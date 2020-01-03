using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abc.Zerio.Core;

namespace Abc.Zerio.Tcp
{
    internal class TcpFrameReceiver
    {
        private readonly Socket _socket;
        private readonly MessageFramer _framer = new MessageFramer();
        private readonly byte[] _buffer = new byte[64 * 1024];

        public event ClientMessageReceivedDelegate MessageReceived;

        public TcpFrameReceiver(Socket socket)
        {
            _socket = socket;
            _socket.ReceiveBufferSize = _buffer.Length;
            _framer.MessageFramed += message => MessageReceived(message);
        }

        public void StartReceive()
        {
            Task.Factory.StartNew(ReceptionLoop, TaskCreationOptions.LongRunning);
        }

        private void ReceptionLoop()
        {
            int byteRead;
            while ((byteRead = _socket.Receive(_buffer)) != -1)
            {
                _framer.SubmitBytes(new Span<byte>(_buffer, 0, byteRead));
            }
        }
    }
}
