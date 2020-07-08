using System;
using System.Linq;
using System.Threading;
using Abc.Zerio.Channel;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;

namespace Abc.Zerio
{
    internal class SendingProcessor
    {
        private bool _isRunning;
        private Thread _sendingThread;

        private readonly ISession[] _sessions;

        public SendingProcessor(ISessionManager sessionManager)
        {
            _sessions = sessionManager.Sessions.ToArray();
            SubscribeToSessionChannels(sessionManager);
        }

        private static void SubscribeToSessionChannels(ISessionManager sessionManager)
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
            var spinWait = new SpinWait();
            while (_isRunning)
            {
                var pollSucceededAtLeastOnce = true;

                foreach (var session in _sessions)
                {
                    pollSucceededAtLeastOnce &= session.SendingChannel.TryPoll();
                }

                if (!pollSucceededAtLeastOnce)
                {
                    spinWait.SpinOnce();
                    continue;
                }

                spinWait.Reset();
            }
        }

        private unsafe static void OnSendRequest(ISession session, ChannelFrame frame, bool isEndOfBatch, bool cleanupNeeded)
        { 
            var bufferSegmentDescriptor = session.SendingChannel.CreateBufferSegmentDescriptor(frame);
      
            session.RequestQueue.Send(cleanupNeeded, &bufferSegmentDescriptor, true);
        }

        public void Stop()
        {
            _isRunning = false;
            _sendingThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
