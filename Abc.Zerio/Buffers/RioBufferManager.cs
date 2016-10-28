using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Buffers
{
    public unsafe class RioBufferManager : IRioBufferReleaser, IDisposable
    {
        private readonly ConcurrentStack<int> _freeBufferIds = new ConcurrentStack<int>(); // todo: use 0 alloc collection
        private readonly IntPtr _underlyingBuffer;
        private readonly IntPtr _bufferDescriptors;
        private readonly IntPtr _bufferId;
        private readonly RioBuffer[] _buffers;

        private volatile bool _isAcquiringCompleted;

        public static RioBufferManager Allocate(int bufferCount, int bufferLength)
        {
            return new RioBufferManager(bufferCount, bufferLength);
        }

        private RioBufferManager(int bufferCount, int bufferLength)
        {
            if (bufferLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferLength));

            if (bufferCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferCount));

            var underlyingBufferLength = (uint)(bufferLength * bufferCount);

            _underlyingBuffer = Kernel32.VirtualAlloc(IntPtr.Zero, underlyingBufferLength, Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE, Kernel32.Consts.PAGE_READWRITE);
            _bufferDescriptors = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)(Marshal.SizeOf<RIO_BUF>() * bufferCount), Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE, Kernel32.Consts.PAGE_READWRITE);

            _bufferId = WinSock.Extensions.RegisterBuffer(_underlyingBuffer, underlyingBufferLength);
            if (_bufferId == WinSock.Consts.RIO_INVALID_BUFFERID)
                WinSock.ThrowLastWsaError();

            _buffers = new RioBuffer[bufferCount];

            var dataPointer = (byte*)_underlyingBuffer.ToPointer();
            var segmentPointer = (RIO_BUF*)_bufferDescriptors.ToPointer();

            for (var segmentIndex = 0; segmentIndex < bufferCount; segmentIndex++)
            {
                segmentPointer->BufferId = _bufferId;
                segmentPointer->Offset = segmentIndex * bufferLength;
                segmentPointer->Length = bufferLength;

                var segment = new RioBuffer(segmentIndex, dataPointer, segmentPointer, bufferLength);

                _buffers[segmentIndex] = segment;
                _freeBufferIds.Push(segment.Id);

                segmentPointer++;
                dataPointer += bufferLength;
            }
        }

        public RioBuffer AcquireBuffer(TimeSpan timeout)
        {
            if (_isAcquiringCompleted)
                throw new InvalidOperationException("Segment acquiring completed.");

            RioBuffer buffer;

            var spinWait = new SpinWait();
            var stopwatch = Stopwatch.StartNew();

            bool bufferAcquired;
            while (!(bufferAcquired = TryAcquireBuffer(out buffer)) && stopwatch.Elapsed < timeout)
            {
                if (_isAcquiringCompleted)
                    throw new InvalidOperationException("Segment acquiring completed.");

                spinWait.SpinOnce();
            }

            if (!bufferAcquired)
                throw new TimeoutException("Timeout occured during buffer segment acquiring for a send operation.");

            return buffer;
        }

        private bool TryAcquireBuffer(out RioBuffer buffer)
        {
            buffer = null;
            int segmentId;
            if (!_freeBufferIds.TryPop(out segmentId))
                return false;

            buffer = _buffers[segmentId];
            return true;
        }

        public void ReleaseBuffer(RioBuffer buffer)
        {
            buffer.Reset();

            _freeBufferIds.Push(buffer.Id);
        }

        public RioBuffer ReadBuffer(int bufferId)
        {
            return _buffers[bufferId];
        }

        public void Reset()
        {
            _isAcquiringCompleted = false;

            _freeBufferIds.Clear();
            for (var segmentIndex = 0; segmentIndex < _buffers.Length; segmentIndex++)
            {
                _freeBufferIds.Push(segmentIndex);
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        ~RioBufferManager()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                WinSock.Extensions.DeregisterBuffer(_bufferId);
            }
            finally
            {
                Kernel32.VirtualFree(_underlyingBuffer, 0, Kernel32.Consts.MEM_RELEASE);
                Kernel32.VirtualFree(_bufferDescriptors, 0, Kernel32.Consts.MEM_RELEASE);
            }
        }

        public void CompleteAcquiring()
        {
            _isAcquiringCompleted = true;
        }
    }
}
