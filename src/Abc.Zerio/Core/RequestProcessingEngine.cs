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

        private readonly UnmanagedRingBuffer<RequestEntry> _ringBuffer;
        private readonly UnmanagedDisruptor<RequestEntry> _disruptor;

        public RequestProcessingEngine(IZerioConfiguration configuration, RioCompletionQueue sendingCompletionQueue, ISessionManager sessionManager)
        {
            _configuration = configuration;

            var entryCount = _configuration.SendingBufferCount;
            _unmanagedRioBuffer = new UnmanagedRioBuffer<RequestEntry>(entryCount, _configuration.SendingBufferLength);

            _disruptor = CreateDisruptor(sendingCompletionQueue, sessionManager);
            _ringBuffer = _disruptor.RingBuffer;
        }

        private unsafe UnmanagedDisruptor<RequestEntry> CreateDisruptor(RioCompletionQueue sendingCompletionQueue, ISessionManager sessionManager)
        {
            var disruptor = new UnmanagedDisruptor<RequestEntry>((IntPtr)_unmanagedRioBuffer.FirstEntry,
                                                                 _unmanagedRioBuffer.EntryReservedSpaceSize,
                                                                 _unmanagedRioBuffer.Length,
                                                                 new ThreadPerTaskScheduler(),
                                                                 ProducerType.Multi,
                                                                 new BusySpinWaitStrategy());

            var handlers = new IValueEventHandler<RequestEntry>[]
            {
                new RequestProcessor(_configuration, sessionManager),
                new SendCompletionProcessor(_configuration, sendingCompletionQueue)
            };

            disruptor.HandleEventsWith(handlers);

            return disruptor;
        }

        public void RequestSend(int sessionId, ReadOnlySpan<byte> message)
        {
            var sequence = _ringBuffer.Next();
            try
            {
                ref var sendingEntry = ref _ringBuffer[sequence];
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
                ref var requestEntry = ref _ringBuffer[sequence];
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
