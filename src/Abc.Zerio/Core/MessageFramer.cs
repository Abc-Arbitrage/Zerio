using System;
using System.Runtime.CompilerServices;
using Abc.Zerio.Channel;

namespace Abc.Zerio.Core
{
    internal class MessageFramer
    {
        private ReadState _readState = ReadState.AccumulatingHeader;
        
        private int _readBytes;
        private int _messageLength;
        private int _frameLength;
        
        private readonly byte[] _buffer ;
        
        public event MessageFramedDelegate MessageFramed;

        public delegate void MessageFramedDelegate(ReadOnlySpan<byte> message);

        public MessageFramer(int framingBufferLength)
        {
            _buffer = new byte[framingBufferLength];
        }

        public void Reset()
        {
            _readState = ReadState.AccumulatingHeader;
            _readBytes = 0;
            _messageLength = 0;
        }

        public void SubmitBytes(Span<byte> bytes)
        {
            var offset = 0;
            var bytesTransferred = bytes.Length;

            while (bytesTransferred - offset > 0)
            {
                var allBytesWereConsumed = _readState == ReadState.AccumulatingHeader
                    ? ConsumeHeaderBytes(bytes, bytesTransferred, ref offset)
                    : ConsumeMessageBytes(bytes, bytesTransferred, ref offset);

                if (allBytesWereConsumed)
                    return;
            }
        }

        private bool ConsumeHeaderBytes(Span<byte> bytes, int bytesTransferred, ref int offset)
        {
            var bytesToCopy = Math.Min(ChannelFrame.HeaderLength - _readBytes, bytesTransferred - offset);
            Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bytes[offset], (uint)bytesToCopy);
            _readBytes += bytesToCopy;

            if (_readBytes != ChannelFrame.HeaderLength)
                return true;

            var header = Unsafe.ReadUnaligned<long>(ref _buffer[0]);
            
            var frameLength = ChannelFrame.GetFrameLength(header);
            var frameTypeId = ChannelFrame.GetFrameTypeId(header);
            
            _frameLength = ChannelFrame.GetAlignedFrameLength(frameLength) - ChannelFrame.HeaderLength;
            _messageLength = frameTypeId == ChannelFrame.PaddingFrame ? 0 : ChannelFrame.GetDataLength(frameLength);
            
            offset += bytesToCopy;

            _readState = ReadState.AccumulatingMessage;
            _readBytes = 0;
            return false;
        }

        private bool ConsumeMessageBytes(Span<byte> bytes, int bytesTransferred, ref int offset)
        {
            var bytesToCopy = Math.Min(_frameLength - _readBytes, bytesTransferred - offset);
            Unsafe.CopyBlockUnaligned(ref _buffer[_readBytes], ref bytes[offset], (uint)bytesToCopy);
            _readBytes += bytesToCopy;

            if (_readBytes != _frameLength)
                return true;

            if (_messageLength != 0)
            {
                // message length can be 0 if the received frame was a padding frame
                MessageFramed?.Invoke(new ReadOnlySpan<byte>(_buffer, 0, _messageLength));
            }
            
            offset += bytesToCopy;

            _messageLength = 0;
            _frameLength = 0;
            _readState = ReadState.AccumulatingHeader;
            _readBytes = 0;
            return false;
        }

        private enum ReadState
        {
            AccumulatingHeader,
            AccumulatingMessage,
        }
    }
}
