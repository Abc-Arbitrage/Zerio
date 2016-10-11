using System;

namespace Abc.Zerio.Core
{
    public interface ISessionConfiguration
    {
        int ReceivingBufferCount { get; }
        int SendingBufferCount { get; }

        int ReceivingBufferLength { get; }
        int SendingBufferLength { get; }

        int MaxOutstandingSends { get; }
        int MaxOutstandingReceives { get; }

        TimeSpan BufferAcquisitionTimeout { get; }
    }
}
