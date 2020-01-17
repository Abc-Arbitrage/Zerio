using System;
using System.Runtime.CompilerServices;

namespace Abc.Zerio.Core
{
    internal class MessageFramer
    {
        private ReadState _readState = ReadState.AccumulatingLength;
        private int _readBytes;
        private int _messageLength;
        private readonly byte[] _buffer ;

        public event MessageFramedDelegate MessageFramed;

        public delegate void MessageFramedDelegate(ReadOnlySpan<byte> message);

        public MessageFramer(int framingBufferLength)
        {
            _buffer = new byte[framingBufferLength];
        }

        public void Reset()
        {
            _readState = ReadState.AccumulatingLength;
            _readBytes = 0;
            _messageLength = 0;
        }

        public void SubmitBytes(Span<byte> bytes)
        {
            var offset = 0;
            var bytesTransferred = bytes.Length;

            while (bytesTransferred - offset > 0)
            {
                var allBytesWereConsumed = _readState == ReadState.AccumulatingLength
                    ? ConsumeLengthBytes(bytes, bytesTransferred, ref offset)
                    : ConsumeMessageBytes(bytes, bytesTransferred, ref offset);

                if (allBytesWereConsumed)
                    return;
            }
        }

        private bool ConsumeLengthBytes(Span<byte> bytes, int bytesTransferred, ref int offset)
        {
            var bytesToCopy = Math.Min(sizeof(int) - _readBytes, bytesTransferred - offset);
            Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bytes[offset], (uint)bytesToCopy);
            _readBytes += bytesToCopy;

            if (_readBytes != sizeof(int))
                return true;

            _messageLength = Unsafe.ReadUnaligned<int>(ref _buffer[0]);

            offset += bytesToCopy;

            _readState = ReadState.AccumulatingMessage;
            _readBytes = 0;
            return false;
        }

        private bool ConsumeMessageBytes(Span<byte> bytes, int bytesTransferred, ref int offset)
        {
            var bytesToCopy = Math.Min(_messageLength - _readBytes, bytesTransferred - offset);
            Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bytes[offset], (uint)bytesToCopy);
            _readBytes += bytesToCopy;

            if (_readBytes != _messageLength)
                return true;

            MessageFramed?.Invoke(new ReadOnlySpan<byte>(_buffer, 0, _messageLength));

            offset += bytesToCopy;

            _messageLength = 0;
            _readState = ReadState.AccumulatingLength;
            _readBytes = 0;
            return false;
        }

        private enum ReadState
        {
            AccumulatingLength,
            AccumulatingMessage,
        }
    }
}
