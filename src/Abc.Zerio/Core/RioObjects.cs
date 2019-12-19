using System;
using Abc.Zerio.Configuration;

namespace Abc.Zerio.Core
{
    internal class RioObjects : IDisposable
    {
        public RioObjects(IZerioConfiguration configuration)
        {
            SendingBuffer = new UnmanagedRioBuffer<RequestEntry>(configuration.SendingBufferCount, configuration.SendingBufferLength);
            SendingCompletionQueue = new RioCompletionQueue(configuration.SendingBufferCount);

            ReceivingBuffer = new UnmanagedRioBuffer<RioBufferSegment>(configuration.ReceivingBufferCount, configuration.ReceivingBufferLength);
            ReceivingCompletionQueue = new RioCompletionQueue(configuration.ReceivingBufferCount);
        }

        public UnmanagedRioBuffer<RequestEntry> SendingBuffer { get; }
        public RioCompletionQueue SendingCompletionQueue { get; }

        public UnmanagedRioBuffer<RioBufferSegment> ReceivingBuffer { get; }
        public RioCompletionQueue ReceivingCompletionQueue { get; }

        public void Dispose()
        {
            SendingCompletionQueue.Dispose();
            SendingBuffer.Dispose();
            
            ReceivingCompletionQueue.Dispose();
            ReceivingBuffer.Dispose();
        }
    }
}
