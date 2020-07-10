using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Channel;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio
{
    internal class SendingProcessor
    {
        private readonly InternalZerioConfiguration _configuration;
        private bool _isRunning;
        private Thread _sendingThread;

        private readonly ISession[] _sessions;
        private static RIO_BUF? _currentBufferSegmentDescriptor;

        public SendingProcessor(InternalZerioConfiguration configuration, ISessionManager sessionManager)
        {
            _configuration = configuration;
            _sessions = sessionManager.Sessions.ToArray();
            SubscribeToSessionChannels(sessionManager);
        }

        private void SubscribeToSessionChannels(ISessionManager sessionManager)
        {
            foreach (var session in sessionManager.Sessions)
            {
                session.SendingChannel.FrameRead += (messageBytes, endOfBatch, cleanupNeeded) => OnSendRequest(session, messageBytes, endOfBatch, cleanupNeeded);
            }
        }

        public void Start()
        {
            _isRunning = true;
            _sendingThread = new Thread(PollSendRequests) { IsBackground = true };
            _sendingThread.Start();
        }

        private void PollSendRequests(object _)
        {
            while (_isRunning)
            {
                foreach (var session in _sessions)
                {
                    session.SendingChannel.TryPoll();
                }
            }
        }
        
        private unsafe void OnSendRequest(ISession session, ChannelFrame frame, bool isEndOfBatch, bool cleanupNeeded)
        {
            if (!_configuration.BatchFramesOnSend)
            {
                var descriptor = session.SendingChannel.CreateBufferSegmentDescriptor(frame);
                session.RequestQueue.Send(cleanupNeeded, &descriptor, true);
                return;
            }
                
            if (_currentBufferSegmentDescriptor == null)
            {
                _currentBufferSegmentDescriptor = session.SendingChannel.CreateBufferSegmentDescriptor(frame);
            }
            else
            {
                _currentBufferSegmentDescriptor = new RIO_BUF
                {
                    BufferId = _currentBufferSegmentDescriptor.Value.BufferId,
                    Offset = _currentBufferSegmentDescriptor.Value.Offset,
                    Length = _currentBufferSegmentDescriptor.Value.Length + (int)frame.DataLength,
                };
            }

            if (isEndOfBatch)
            {
                var descriptor = _currentBufferSegmentDescriptor.Value;
                session.RequestQueue.Send(cleanupNeeded, &descriptor, true);
                _currentBufferSegmentDescriptor = null;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _sendingThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
