using System;
using Abc.Zerio.Buffers;
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

        public unsafe void Receive(RioBuffer buffer)
        {
            var requestContextKey = new RioRequestContextKey(buffer.Id, RioRequestType.Receive);

            if (!WinSock.Extensions.Receive(_handle, buffer.BufferDescriptor, 1, RIO_RECEIVE_FLAGS.NONE, requestContextKey.ToRioRequestCorrelationId()))
                WinSock.ThrowLastWsaError();
        }

        public unsafe void Send(RioBuffer buffer)
        {
            var requestContextKey = new RioRequestContextKey(buffer.Id, RioRequestType.Send);

            if (!WinSock.Extensions.Send(_handle, buffer.BufferDescriptor, 1, RIO_SEND_FLAGS.NONE, requestContextKey.ToRioRequestCorrelationId()))
                WinSock.ThrowLastWsaError();
        }
    }
}
