using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Abc.Zerio.Framing
{
    public unsafe class UnsafeBinaryWriter : BinaryWriter
    {
        private BufferSegment[] _buffers = new BufferSegment[1024];
        private int _bufferIndex;
        private int _bufferCount;

        private byte* _bufferPosition;
        private byte* _endOfBuffer;

        private IBufferSegmentProvider _bufferSegmentProvider;

        private readonly Encoding _encoding;

        public UnsafeBinaryWriter(Encoding encoding)
        {
            _encoding = encoding;
        }

        public long GetLength()
        {
            var position = 0L;
            for (var i = 0; i < _bufferCount; i++)
            {
                position += _buffers[i].Length;
            }

            position -= _endOfBuffer - _bufferPosition;

            return position;
        }

        internal Position GetPosition()
        {
            return new Position(_bufferIndex, _bufferPosition);
        }

        internal void SetPosition(Position position)
        {
            _bufferIndex = position.BufferIndex;
            _bufferPosition = position.BufferPosition;
            _endOfBuffer = _buffers[position.BufferIndex].EndOfBuffer;
        }

        public void SetBufferSegmentProvider(IBufferSegmentProvider bufferSegmentProvider)
        {
            _bufferSegmentProvider = bufferSegmentProvider;
            _bufferIndex = -1;
            _bufferCount = 0;

            SwitchToNextBuffer();
        }

        private void SwitchToNextBuffer()
        {
            _bufferIndex++;

            if (_bufferIndex >= _bufferCount)
                AddBufferSegment();

            _bufferPosition = _buffers[_bufferIndex].Data;
            _endOfBuffer = _buffers[_bufferIndex].EndOfBuffer;
        }

        private void AddBufferSegment()
        {
            if (_buffers.Length == _bufferCount)
                Array.Resize(ref _buffers, _bufferCount * 2);

            _buffers[_bufferCount] = _bufferSegmentProvider.GetBufferSegment();
            _bufferCount++;
        }

        public override void Write(bool value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(bool)))
            {
                WriteOverlapped((ulong*)&value, sizeof(bool));
                return;
            }

            *(bool*)_bufferPosition = value;
            _bufferPosition += sizeof(bool);
        }

        public override void Write(byte value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(byte)))
            {
                WriteOverlapped((ulong*)&value, sizeof(byte));
                return;
            }

            *_bufferPosition = value;
            _bufferPosition += sizeof(byte);
        }

        public override void Write(decimal value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(decimal)))
            {
                WriteOverlappedDecimal(value);
                return;
            }

            var proxy = *(DecimalProxy*)&value;
            *(int*)_bufferPosition = proxy.lo;
            _bufferPosition += sizeof(int);
            *(int*)_bufferPosition = proxy.mid;
            _bufferPosition += sizeof(int);
            *(int*)_bufferPosition = proxy.hi;
            _bufferPosition += sizeof(int);
            *(int*)_bufferPosition = proxy.flags;
            _bufferPosition += sizeof(int);
        }

        public override void Write(double value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(double)))
            {
                WriteOverlapped((ulong*)&value, sizeof(double));
                return;
            }

            *(double*)_bufferPosition = value;
            _bufferPosition += sizeof(double);
        }

        public override void Write(float value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(float)))
            {
                WriteOverlapped((ulong*)&value, sizeof(float));
                return;
            }

            *(float*)_bufferPosition = value;
            _bufferPosition += sizeof(float);
        }

        public override void Write(int value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(int)))
            {
                WriteOverlapped((ulong*)&value, sizeof(int));
                return;
            }

            *(int*)_bufferPosition = value;
            _bufferPosition += sizeof(int);
        }

        public override void Write(long value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(long)))
            {
                WriteOverlapped((ulong*)&value, sizeof(long));
                return;
            }

            *(long*)_bufferPosition = value;
            _bufferPosition += sizeof(long);
        }

        public override void Write(sbyte value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(sbyte)))
            {
                WriteOverlapped((ulong*)&value, sizeof(sbyte));
                return;
            }

            *(sbyte*)_bufferPosition = value;
            _bufferPosition += sizeof(sbyte);
        }

        public override void Write(short value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(short)))
            {
                WriteOverlapped((ulong*)&value, sizeof(short));
                return;
            }

            *(short*)_bufferPosition = value;
            _bufferPosition += sizeof(short);
        }

        public override void Write(uint value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(uint)))
            {
                WriteOverlapped((ulong*)&value, sizeof(uint));
                return;
            }

            *(uint*)_bufferPosition = value;
            _bufferPosition += sizeof(uint);
        }

        public override void Write(ulong value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(ulong)))
            {
                WriteOverlapped(&value, sizeof(ulong));
                return;
            }

            *(ulong*)_bufferPosition = value;
            _bufferPosition += sizeof(ulong);
        }

        public override void Write(ushort value)
        {
            if (!CurrentBufferHasEnoughBytes(sizeof(ushort)))
            {
                WriteOverlapped((ulong*)&value, sizeof(ushort));
                return;
            }

            *(ushort*)_bufferPosition = value;
            _bufferPosition += sizeof(ushort);
        }

        public override void Write(string value)
        {
            var byteCount = _encoding.GetByteCount(value);

            Write7BitEncodedInt(byteCount);

            fixed (char* pChars = value)
            {
                WriteOverlappedChars(pChars, 0, value.Length);
            }
        }

        public override void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            fixed (byte* pBytes = buffer)
            {
                var remainingCount = count;
                while (true)
                {
                    var byteCount = WriteBytesInCurrentBuffer(pBytes, index, remainingCount);

                    remainingCount -= byteCount;
                    if (remainingCount == 0)
                        return;

                    SwitchToNextBuffer();
                    index += byteCount;
                }
            }
        }

        private void WriteOverlappedChars(char* pChars, int index, int count)
        {
            var remainingCount = count;
            while (true)
            {
                var charCount = WriteCharsInCurrentBuffer(pChars, index, remainingCount);

                remainingCount -= charCount;
                if (remainingCount == 0)
                    return;

                SwitchToNextBuffer();
                index += charCount;
            }
        }

        private int WriteCharsInCurrentBuffer(char* pChars, int index, int remainingCount)
        {
            var availableData = (int)(_endOfBuffer - _bufferPosition);

            var pCharWithOffset = pChars + index;

            var byteCount = _encoding.GetByteCount(pCharWithOffset, remainingCount);

            if (_bufferPosition + byteCount > _endOfBuffer)
                throw new InvalidOperationException("overlapped char writes not supported yet");

            _encoding.GetBytes(pCharWithOffset, remainingCount, _bufferPosition, availableData);

            _bufferPosition += byteCount;

            return remainingCount;
        }

        private int WriteBytesInCurrentBuffer(byte* pBytes, int index, int remainingCount)
        {
            var availableData = (int)(_endOfBuffer - _bufferPosition);
            var bytesToWrite = Math.Min(remainingCount, availableData);
            Buffer.MemoryCopy(pBytes + index, _bufferPosition, bytesToWrite, bytesToWrite);
            _bufferPosition += bytesToWrite;
            return bytesToWrite;
        }

        public override void Write(char ch)
        {
            WriteOverlappedChars(&ch, 0, 1);
        }

        public override void Write(char[] chars)
        {
            Write(chars, 0, chars.Length);
        }

        public override void Write(char[] chars, int index, int count)
        {
            fixed (char* pChars = chars)
            {
                WriteOverlappedChars(pChars, index, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CurrentBufferHasEnoughBytes(int size)
        {
            return _bufferPosition + size <= _endOfBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteOverlapped(ulong* value, int sizeOfType)
        {
            var byteWritten = 0;
            while (byteWritten < sizeOfType)
            {
                if (_bufferPosition == _endOfBuffer)
                    SwitchToNextBuffer();

                var b = (byte)(*value >> (byteWritten * 8));
                byteWritten++;
                *_bufferPosition++ = b;
            }
        }

        private void WriteOverlappedDecimal(decimal value)
        {
            var proxy = *(DecimalProxy*)&value;
            var lo = proxy.lo;
            WriteOverlapped((ulong*)&lo, sizeof(int));
            var mid = proxy.mid;
            WriteOverlapped((ulong*)&mid, sizeof(int));
            var hi = proxy.hi;
            WriteOverlapped((ulong*)&hi, sizeof(int));
            var flags = proxy.flags;
            WriteOverlapped((ulong*)&flags, sizeof(int));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DecimalProxy
        {
            public readonly int flags;
            public readonly int hi;
            public readonly int lo;
            public readonly int mid;
        }

        internal struct Position
        {
            public readonly int BufferIndex;
            public readonly byte* BufferPosition;

            public Position(int bufferIndex, byte* bufferPosition)
            {
                BufferIndex = bufferIndex;
                BufferPosition = bufferPosition;
            }
        }
    }
}
