using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Configuration;
using Disruptor;
using Disruptor.Dsl;

namespace Abc.Zerio.Core
{
    internal class RequestProcessingEngine : IDisposable
    {
        private readonly IZerioConfiguration _configuration;
        private readonly UnmanagedRioBuffer<RequestEntry> _unmanagedRioBuffer;

        private readonly ValueRingBuffer<RequestEntry> _ringBuffer;
        private readonly ValueDisruptor<RequestEntry> _disruptor;

        public RequestProcessingEngine(IZerioConfiguration configuration, RioObjects rioObjects, ISessionManager sessionManager)
        {
            _configuration = configuration;
            _unmanagedRioBuffer = rioObjects.SendingBuffer;
            _disruptor = CreateDisruptor(rioObjects, sessionManager);
            _ringBuffer = _disruptor.RingBuffer;
        }

        private ValueDisruptor<RequestEntry> CreateDisruptor(RioObjects rioObjects, ISessionManager sessionManager)
        {
            // todo: use _requestingEntryBuffer with the next release of ValueDisruptor
            var disruptor = new ValueDisruptor<RequestEntry>(() => new RequestEntry() ,
                                                       _configuration.SendingBufferCount,
                                                       new ThreadPerTaskScheduler(),
                                                       ProducerType.Multi,
                                                       new BusySpinWaitStrategy());

            var handlers = new IValueEventHandler<RequestEntry>[]
            {
                new RequestProcessor(_configuration, sessionManager, rioObjects.ReceivingBuffer),
                new SendCompletionProcessor(_configuration, rioObjects.SendingCompletionQueue)
            };

            disruptor.HandleEventsWith(handlers);

            return disruptor;
        }

        public void RequestSend(int sessionId, ReadOnlySpan<byte> message)
        {
            var sequence = _ringBuffer.Next();
            try
            {
                var sendingEntry = _ringBuffer[sequence];
                sendingEntry.SetWriteRequest(sessionId, message);
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }

        public void RequestReceive(int sessionId, int bufferSegmentId)
        {
            var sequence = _ringBuffer.Next();
            try
            {
                var requestEntry = _ringBuffer[sequence];
                requestEntry.SetReadRequest(sessionId, bufferSegmentId);
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }

        public void Start()
        {
            _disruptor.Start();
        }

        public void Stop()
        {
            _disruptor.Shutdown();
        }

        public void Dispose()
        {
            Stop();

            _unmanagedRioBuffer?.Dispose();
        }

        private class ThreadPerTaskScheduler : TaskScheduler
        { 
            protected override IEnumerable<Task> GetScheduledTasks() { return Enumerable.Empty<Task>(); }
      
            protected override void QueueTask(Task task)
            {
                new Thread(() => TryExecuteTask(task)) { IsBackground = true }.Start();
            }
 
            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                return TryExecuteTask(task);
            }
        }
    }
}
