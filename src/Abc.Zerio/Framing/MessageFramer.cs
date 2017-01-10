using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Abc.Zerio.Buffers;

namespace Abc.Zerio.Framing
{
    public unsafe class MessageFramer
    {
        private readonly IRioBufferReleaser _releaser;
        private readonly List<BufferSegment> _currentFrame = new List<BufferSegment>();
        private readonly List<RioBuffer> _pendingBuffers = new List<RioBuffer>();

        private RioBuffer _currentBuffer;
        private BufferSegment _currentBufferSegment;
        private ByteCount _remainingFrameByteCount;
        private byte* _currentRegisteredSegmentEnd;

        public MessageFramer(IRioBufferReleaser releaser)
        {
            _releaser = releaser;
        }

        public bool TryFrameNextMessage(RioBuffer rioBuffer, out List<BufferSegment> frame)
        {
            frame = null;

            var currentSegmentIsCompleted = _currentRegisteredSegmentEnd == _currentBufferSegment.Data;

            if (_remainingFrameByteCount.IsEmpty)
            {
                // the previous frame was framed entirely, we can reset it
                _currentFrame.Clear();

                // we can release all pending buffers, but the current one must be kept if it is not entirely read
                ReleasePendingBuffers(currentSegmentIsCompleted);

                if (currentSegmentIsCompleted && _currentBuffer != null)
                {
                    _currentRegisteredSegmentEnd = rioBuffer.Data;
                    return false;
                }
            }

            if (rioBuffer != _currentBuffer)
            {
                // a new buffer is submitted, we initialize a new frame segment
                _currentBufferSegment = new BufferSegment(rioBuffer.Data);
                _currentBuffer = rioBuffer;
                _pendingBuffers.Add(_currentBuffer);
            }

            _currentRegisteredSegmentEnd = rioBuffer.Data + rioBuffer.DataLength;

            // we need to read the length prefix of the frame, so we loop until we have the 4 bytes of data we need
            while (!_remainingFrameByteCount.IsComplete)
            {
                if (_currentBufferSegment.Data >= _currentRegisteredSegmentEnd)
                    return false;

                _remainingFrameByteCount.Push(_currentBufferSegment.Data);
                _currentBufferSegment = new BufferSegment(_currentBufferSegment.Data + 1, _currentBufferSegment.Length);
            }

            // we just finished to read the frame length and the buffer is completed, we can release it right away
            if (_currentFrame.Count == 0)
                ReleasePendingBuffers(_currentBufferSegment.Data == _currentRegisteredSegmentEnd);

            // at this point, we know the actual frame length
            // if the frame bytes span beyond the end of the current buffer, we trim the current frame part
            // to the end of the buffer and we return so that we can be provided another buffer
            var frameEnd = _currentBufferSegment.Data + _remainingFrameByteCount.Value;
            if (frameEnd > _currentRegisteredSegmentEnd)
            {
                var segmentLength = _currentBufferSegment.Length + (int)(_currentRegisteredSegmentEnd - _currentBufferSegment.Data);
                _currentBufferSegment = new BufferSegment(_currentBufferSegment.Data, segmentLength);
                _remainingFrameByteCount.Value -= _currentBufferSegment.Length;
                _currentFrame.Add(_currentBufferSegment);
                return false;
            }

            // if the buffer contains enough data, we take as much as we need and mark the frame as completed
            _currentBufferSegment = new BufferSegment(_currentBufferSegment.Data, _currentBufferSegment.Length + _remainingFrameByteCount.Value);
            _currentFrame.Add(_currentBufferSegment);

            _currentBufferSegment = new BufferSegment(_currentBufferSegment.Data + _currentBufferSegment.Length);
            _remainingFrameByteCount = ByteCount.Empty;

            frame = _currentFrame;
            return true;
        }

        private void ReleasePendingBuffers(bool releaseCurrentSegment)
        {
            for (var i = _pendingBuffers.Count - 1; i >= 0; i--)
            {
                var bufferSegment = _pendingBuffers[i];
                if (bufferSegment == _currentBuffer && !releaseCurrentSegment)
                    continue;

                _pendingBuffers.RemoveAt(i);
                _releaser.ReleaseBuffer(bufferSegment);
            }
        }

        private struct ByteCount
        {
            private byte _frameLengthBytesRead;

            public int Value;

            public bool IsEmpty => _frameLengthBytesRead == 0;
            public bool IsComplete => _frameLengthBytesRead == sizeof(int);

            public static readonly ByteCount Empty = new ByteCount();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(byte* b)
            {
                Value |= *b << (_frameLengthBytesRead++ * 8);
            }
        }

        public void Reset()
        {
            _remainingFrameByteCount = ByteCount.Empty;
            _currentRegisteredSegmentEnd = (byte*)0;
            _currentBuffer = null;
            _currentFrame.Clear();
            _pendingBuffers.Clear();
        }
    }
}
