using System;
using System.Threading;
using Abc.Zerio.Channel;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    internal class RioRequestQueue
    {
        private readonly IntPtr _handle;

        private RioRequestQueue(IntPtr handle)
        {
            _handle = handle;
        }

        public static RioRequestQueue Create(int correlationId, IntPtr socket, IRioCompletionQueue sendingCompletionQueue, uint maxOutstandingSends, IRioCompletionQueue receivingCompletionQueue, uint maxOutstandingReceives)
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

                var rioReceiveFlags = flush ? RIO_RECEIVE_FLAGS.NONE : RIO_RECEIVE_FLAGS.DEFER;

                rioReceiveFlags |= RIO_RECEIVE_FLAGS.DONT_NOTIFY;
                
                if (!WinSock.Extensions.Receive(_handle, bufferSegment->GetRioBufferDescriptor(), 1, rioReceiveFlags, bufferSegmentId))
                    WinSock.ThrowLastWsaError();
            }
            finally
            {
                if (lockTaken)
                    spinlock.Exit();
            }
        }

        public unsafe void Send(SendCompletionToken sendCompletionToken, RIO_BUF* bufferSegmentDescriptor, bool flush)
        {
            var lockTaken = false;
            var spinlock = new SpinLock();
            try
            {
                spinlock.Enter(ref lockTaken);

                var rioSendFlags = flush ? RIO_SEND_FLAGS.NONE : RIO_SEND_FLAGS.DEFER;

                rioSendFlags |= RIO_SEND_FLAGS.DONT_NOTIFY;
                
                if (!WinSock.Extensions.Send(_handle, bufferSegmentDescriptor, 1, rioSendFlags, (long)sendCompletionToken))
                    WinSock.ThrowLastWsaError();
            }
            finally
            {
                if (lockTaken)
                    spinlock.Exit();
            }
        }

        public unsafe void FlushSends()
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
