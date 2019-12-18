using System;
using Abc.Zerio.Interop;

namespace Abc.Zerio.Core
{
    public class RioRequestQueue
    {
        private readonly IntPtr _handle;

        private RioRequestQueue(IntPtr handle)
        {
            _handle = handle;
        }

        public static RioRequestQueue Create(int correlationId, IntPtr socket, RioCompletionQueue sendingCompletionQueue, uint maxOutstandingSends, RioCompletionQueue receivingCompletionQueue, uint maxOutstandingReceives)
        {
            var requestQueue = WinSock.Extensions.CreateRequestQueue(socket, maxOutstandingReceives, 1, maxOutstandingSends, 1, receivingCompletionQueue.QueueHandle, sendingCompletionQueue.QueueHandle, correlationId);
            if (requestQueue == IntPtr.Zero)
                WinSock.ThrowLastWsaError();

            return new RioRequestQueue(requestQueue);
        }

        public unsafe void Receive(RioBufferSegment* bufferSegment, int bufferSegmentId)
        {
            if (!WinSock.Extensions.Receive(_handle, bufferSegment->GetRioBufferDescriptor(), 1, RIO_RECEIVE_FLAGS.NONE, bufferSegmentId))
                WinSock.ThrowLastWsaError();
        }

        public unsafe void Send(long sequence, RIO_BUF* bufferSegmentDescriptor, bool flush)
        {
            var rioSendFlags = flush ? RIO_SEND_FLAGS.NONE : RIO_SEND_FLAGS.DEFER;

            if (!WinSock.Extensions.Send(_handle, bufferSegmentDescriptor, 1, rioSendFlags, sequence))
                WinSock.ThrowLastWsaError();
        }

        public unsafe void Flush()
        {
            if (!WinSock.Extensions.Send(_handle, null, 1, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                WinSock.ThrowLastWsaError();
        }
    }
}
