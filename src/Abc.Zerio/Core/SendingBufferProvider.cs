using System;
using System.Collections.Generic;
using Abc.Zerio.Buffers;
using Abc.Zerio.Framing;

namespace Abc.Zerio.Core
{
    public class SendingBufferProvider : IMessageBufferSegmentProvider
    {
        private readonly ISessionConfiguration _configuration;
        private readonly RioBufferManager _bufferManager;
        private readonly List<RioBuffer> _buffers = new List<RioBuffer>();

        public SendingBufferProvider(ISessionConfiguration configuration, RioBufferManager bufferManager)
        {
            _configuration = configuration;
            _bufferManager = bufferManager;
        }

        public void Reset()
        {
            _buffers.Clear();
        }

        public List<RioBuffer>.Enumerator GetEnumerator() => _buffers.GetEnumerator();

        public unsafe BufferSegment GetBufferSegment()
        {
            var registeredBufferSegment = _bufferManager.AcquireBuffer(_configuration.BufferAcquisitionTimeout);
            _buffers.Add(registeredBufferSegment);
            return new BufferSegment(registeredBufferSegment.Data, registeredBufferSegment.Length);
        }

        public void SetMessageLength(int messageLength)
        {
            for (var bufferIndex = 0; bufferIndex < _buffers.Count; bufferIndex++)
            {
                var currentSegment = _buffers[bufferIndex];

                currentSegment.DataLength = Math.Min(currentSegment.Length, messageLength);
                messageLength -= currentSegment.DataLength;
            }
        }
    }
}
