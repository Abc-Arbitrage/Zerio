using System;
using System.Threading;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    public class RioRequestQueue
    {
        private readonly IntPtr _handle;

        public Action FlushSendsOperation { get; }

        private RioRequestQueue(IntPtr handle)
        {
            _handle = handle;
            FlushSendsOperation = FlushSends;
        }

        public static RioRequestQueue Create(int correlationId, IntPtr socket, RioCompletionQueue sendingCompletionQueue, uint maxOutstandingSends, RioCompletionQueue receivingCompletionQueue, uint maxOutstandingReceives)
        {
            var requestQueue = WinSock.Extensions.CreateRequestQueue(socket, maxOutstandingReceives, 1, maxOutstandingSends, 1, receivingCompletionQueue.QueueHandle, sendingCompletionQueue.QueueHandle, correlationId);
            if (requestQueue == IntPtr.Zero)
                WinSock.ThrowLastWsaError();

            return new RioRequestQueue(requestQueue);
        }

        public unsafe void Receive(RioBufferSegment* bufferSegment, int bufferSegmentId, bool flush)
        {
            var lockTaken = false;
            var spinlock = new SpinLock();
            try
            {
                spinlock.Enter(ref lockTaken);

                var rioSendFlags = flush ? RIO_RECEIVE_FLAGS.NONE : RIO_RECEIVE_FLAGS.DEFER;

                if (!WinSock.Extensions.Receive(_handle, bufferSegment->GetRioBufferDescriptor(), 1, rioSendFlags, bufferSegmentId))
                    WinSock.ThrowLastWsaError();
            }
            finally
            {
                if (lockTaken)
                    spinlock.Exit();
            }
        }

        public unsafe void Send(long sequence, RIO_BUF* bufferSegmentDescriptor, bool flush)
        {
            var lockTaken = false;
            var spinlock = new SpinLock();
            try
            {
                spinlock.Enter(ref lockTaken);

                var rioSendFlags = flush ? RIO_SEND_FLAGS.NONE : RIO_SEND_FLAGS.DEFER;

                if (!WinSock.Extensions.Send(_handle, bufferSegmentDescriptor, 1, rioSendFlags, sequence))
                    WinSock.ThrowLastWsaError();
            }
            finally
            {
                if (lockTaken)
                    spinlock.Exit();
            }
        }

        private unsafe void FlushSends()
        {
            var lockTaken = false;
            var spinlock = new SpinLock();
            try
            {
                spinlock.Enter(ref lockTaken);

                if (!WinSock.Extensions.Send(_handle, null, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                    WinSock.ThrowLastWsaError();
            }
            finally
            {
                if (lockTaken)
                    spinlock.Exit();
            }
        }
    }
}
