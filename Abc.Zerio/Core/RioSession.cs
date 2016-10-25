using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Abc.Zerio.Buffers;
using Abc.Zerio.Framing;
using Abc.Zerio.Interop;
using Abc.Zerio.Serialization;

namespace Abc.Zerio.Core
{
    public class RioSession : IDisposable
    {
        public int Id { get; }

        private readonly ISessionConfiguration _configuration;
        private readonly RioBufferManager _sendingBufferManager;
        private readonly RioBufferManager _receivingBufferManager;
        private readonly RioCompletionQueue _receivingCompletionQueue;
        private readonly RioCompletionQueue _sendingCompletionQueue;

        private IntPtr _socket;

        private RioRequestQueue _requestQueue;

        private readonly SerializationEngine _serializationEngine;
        private readonly MessageFramer _messageFramer;

        private readonly ThreadLocal<ReceivingContext> _threadLocalReceivingContext;
        private readonly ThreadLocal<SendingContext> _threadLocalSendingContext;

        private int _pendingSendCount;
        private int _pendingReceiveCount;

        public event Action<RioSession, MessageTypeId, object> MessageReceived = delegate { };
        public event Action<RioSession> Closed = delegate { };

        public RioSession(int sessionId, ISessionConfiguration configuration, RioCompletionQueue sendingCompletionQueue, RioCompletionQueue receivingCompletionQueue, SerializationEngine serializationEngine)
        {
            Id = sessionId;
            _configuration = configuration;

            _sendingCompletionQueue = sendingCompletionQueue;
            _receivingCompletionQueue = receivingCompletionQueue;

            _sendingBufferManager = RioBufferManager.Allocate(configuration.SendingBufferCount, _configuration.SendingBufferLength);
            _receivingBufferManager = RioBufferManager.Allocate(configuration.ReceivingBufferCount, _configuration.ReceivingBufferLength);

            _messageFramer = new MessageFramer(_receivingBufferManager);

            _threadLocalReceivingContext = new ThreadLocal<ReceivingContext>(() => new ReceivingContext(serializationEngine.Encoding));
            _threadLocalSendingContext = new ThreadLocal<SendingContext>(() => new SendingContext(configuration, _sendingBufferManager, serializationEngine.Encoding));

            _serializationEngine = serializationEngine;
        }

        public void Open(IntPtr socket)
        {
            Close();

            _socket = socket;

            var maxOutstandingReceives = (uint)_configuration.MaxOutstandingReceives;
            var maxOutstandingSends = (uint)_configuration.MaxOutstandingSends;

            _requestQueue = RioRequestQueue.Create(Id, socket, _sendingCompletionQueue, maxOutstandingSends, _receivingCompletionQueue, maxOutstandingReceives);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _requestQueue, null) == null)
                return;

            _sendingBufferManager.CompleteAcquiring();
            _receivingBufferManager.CompleteAcquiring();

            WaitForPendingSendAndReceive();

            WinSock.closesocket(_socket);
            _socket = IntPtr.Zero;

            _sendingBufferManager.Reset();
            _receivingBufferManager.Reset();
            _messageFramer.Reset();

            Closed(this);
        }

        private void WaitForPendingSendAndReceive()
        {
            var timeout = TimeSpan.FromSeconds(5);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (Volatile.Read(ref _pendingSendCount) == 0 && Volatile.Read(ref _pendingReceiveCount) == 0)
                    return;

                Thread.Sleep(50);
            }
        }

        public void EnqueueSend(object message)
        {
            Interlocked.Increment(ref _pendingSendCount);
            try
            {
                var requestQueue = Volatile.Read(ref _requestQueue);
                if (requestQueue == null)
                    return;

                var sendingContext = _threadLocalSendingContext.Value;
                var sendingBufferProvider = sendingContext.SendingBufferProvider;

                try
                {
                    _serializationEngine.SerializeWithLengthPrefix(message, sendingBufferProvider, sendingContext.BinaryWriter);

                    foreach (var buffer in sendingBufferProvider)
                    {
                        requestQueue.Send(buffer);
                    }
                }
                finally
                {
                    sendingBufferProvider.Reset();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _pendingSendCount);
            }
        }

        private void EnqueueReceive()
        {
            Interlocked.Increment(ref _pendingReceiveCount);
            try
            {
                var requestQueue = Volatile.Read(ref _requestQueue);
                if (requestQueue == null)
                    return;

                var bufferSegment = _receivingBufferManager.AcquireBuffer(_configuration.BufferAcquisitionTimeout);

                requestQueue.Receive(bufferSegment);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingReceiveCount);
            }
        }

        private void OnSendComplete(RioRequestContextKey requestContextKey, int bytesTransferred)
        {
            var buffer = _sendingBufferManager.ReadBuffer(requestContextKey.BufferId);
            if (buffer.DataLength != bytesTransferred)
            {
                // this is an abnormal incomplete send; we disconnect the session
                Close();
                return;
            }

            _sendingBufferManager.ReleaseBuffer(buffer);
        }

        private void OnReceiveComplete(RioRequestContextKey requestContextKey, int bytesTransferred)
        {
            var buffer = _receivingBufferManager.ReadBuffer(requestContextKey.BufferId);

            if (buffer.Length < bytesTransferred)
                throw new InvalidOperationException("Received more bytes than expected");

            buffer.DataLength = bytesTransferred;

            OnReceiveComplete(buffer);

            EnqueueReceive();
        }

        private void OnReceiveComplete(RioBuffer buffer)
        {
            var receivingContext = _threadLocalReceivingContext.Value;

            List<BufferSegment> framedMessage;
            while (_messageFramer.TryFrameNextMessage(buffer, out framedMessage))
            {
                var releasableMessage = _serializationEngine.DeserializeWithLengthPrefix(framedMessage, receivingContext.BinaryReader);
                MessageReceived(this,  releasableMessage.MessageTypeId, releasableMessage.Message);
                releasableMessage.Releaser?.Release(releasableMessage.Message);
            }
        }

        public void OnRequestCompletion(RioRequestContextKey requestContextKey, int bytesTransferred)
        {
            if (bytesTransferred == 0)
            {
                Close();
                return;
            }

            switch (requestContextKey.RequestType)
            {
                case RioRequestType.Send:
                    OnSendComplete(requestContextKey, bytesTransferred);
                    break;
                case RioRequestType.Receive:
                    OnReceiveComplete(requestContextKey, bytesTransferred);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(RioRequestContextKey.RequestType));
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        ~RioSession()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            Close();

            if (disposing)
            {
                _sendingBufferManager.Dispose();
                _receivingBufferManager.Dispose();
            }
        }

        public void InitiateReceiving()
        {
            // Max number of buffers that can be retained by the message framer on reception
            // before being released.
            const int framingMaxPendingBufferCount = 4;

            for (var i = 0; i < _configuration.ReceivingBufferCount - framingMaxPendingBufferCount; i++)
            {
                EnqueueReceive();
            }
        }

        private class ReceivingContext
        {
            internal readonly UnsafeBinaryReader BinaryReader;

            public ReceivingContext(Encoding encoding)
            {
                BinaryReader = new UnsafeBinaryReader(encoding);
            }
        }

        private class SendingContext
        {
            internal readonly UnsafeBinaryWriter BinaryWriter;
            internal readonly SendingBufferProvider SendingBufferProvider;

            public SendingContext(ISessionConfiguration configuration, RioBufferManager sendingBufferManager, Encoding encoding)
            {
                SendingBufferProvider = new SendingBufferProvider(configuration, sendingBufferManager);
                BinaryWriter = new UnsafeBinaryWriter(encoding);
            }
        }
    }
}
