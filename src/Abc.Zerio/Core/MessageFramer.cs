using System;
using System.Runtime.CompilerServices;

namespace Abc.Zerio.Core
{
    internal class MessageFramer
    {
        private ReadState _readState = ReadState.AccumulatingLength;
        private int _readBytes;
        private int _messageLength;
        private readonly byte[] _buffer = new byte[64 * 1024];

        public event MessageFramedDelegate MessageFramed;

        public delegate void MessageFramedDelegate(ReadOnlySpan<byte> message);
        
        public void Reset()
        {
            _readState = ReadState.AccumulatingLength;
            _readBytes = 0;
            _messageLength = 0;
        }

        public unsafe void SubmitBytes(RioBufferSegment* bufferSegment, in int bytesTransferred)
        {
            var offset = 0;

            var bufferSegmentStart = bufferSegment->GetBufferSegmentStart();
            while (bytesTransferred - offset > 0)
            {
                switch (_readState)
                {
                    case ReadState.AccumulatingLength:
                    {
                        var bytesToCopy = Math.Min(sizeof(int) - _readBytes, bytesTransferred - offset);
                        Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bufferSegmentStart[offset], (uint)bytesToCopy);
                        _readBytes += bytesToCopy;

                        if (_readBytes != sizeof(int))
                            return;

                        _messageLength = Unsafe.ReadUnaligned<int>(ref _buffer[0]);

                        offset += bytesToCopy;

                        _readState = ReadState.AccumulatingMessage;
                        _readBytes = 0;
                        continue;
                    }

                    case ReadState.AccumulatingMessage:
                    {
                        var bytesToCopy = Math.Min(_messageLength - _readBytes, bytesTransferred - offset);
                        Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bufferSegmentStart[offset], (uint)bytesToCopy);
                        _readBytes += bytesToCopy;

                        if (_readBytes != _messageLength)
                            return;

                        MessageFramed?.Invoke(new ReadOnlySpan<byte>(_buffer, 0, _messageLength));

                        offset += bytesToCopy;

                        _messageLength = 0;
                        _readState = ReadState.AccumulatingLength;
                        _readBytes = 0;
                        continue;
                    }
                }
            }
        }

        private enum ReadState
        {
            AccumulatingLength,
            AccumulatingMessage,
        }
    }
}
