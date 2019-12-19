using System;
using Abc.Zerio.Configuration;

namespace Abc.Zerio.Core
{
    internal class RegisteredBuffers : IDisposable
    {
        public readonly UnmanagedRioBuffer<RequestEntry> SendingBuffer;
        public readonly UnmanagedRioBuffer<RioBufferSegment> ReceivingBuffer;

        public RegisteredBuffers(IZerioConfiguration configuration)
        {
            SendingBuffer = new UnmanagedRioBuffer<RequestEntry>(configuration.SendingBufferCount, configuration.SendingBufferLength);    
            ReceivingBuffer = new UnmanagedRioBuffer<RioBufferSegment>(configuration.ReceivingBufferCount, configuration.ReceivingBufferLength);    
        }

        public void Dispose()
        {
            SendingBuffer?.Dispose();
            ReceivingBuffer?.Dispose();
        }
    }
}
