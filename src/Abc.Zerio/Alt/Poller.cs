using System;
using System.Threading;

namespace Abc.Zerio.Alt
{
    internal class Poller : IDisposable
    {
        // TODO Support WSA events

        private readonly CancellationToken _ct;
        private readonly Session[] _sessions = new Session[64];
        private volatile int _sessionCount;
        private int _lastIdx;
        private readonly Thread _thread;

        public Poller(string name, CancellationToken ct)
        {
            _ct = ct;
            _thread = new Thread(Poll);
            _thread.Priority = ThreadPriority.Highest;
            _thread.Name = name;
            _thread.Start();
        }

        public void AddSession(Session session)
        {
            lock (_sessions)
            {
                for (int i = 0; i < _sessions.Length; i++)
                {
                    if (null == Interlocked.CompareExchange(ref _sessions[i], session, null))
                    {
                        Interlocked.Increment(ref _sessionCount);
                        return;
                    }
                }
            }

            throw new NotSupportedException();
        }

        public void RemoveSession(Session session)
        {
            lock (_sessions)
            {
                int idx = -1;
                for (int i = 0; i < _sessions.Length; i++)
                {
                    if (session == _sessions[i])
                    {
                        idx = i;
                    }
                }

                if (idx == -1)
                    throw new InvalidOperationException("Session is not registered with this poller.");

                Interlocked.Exchange(ref _sessions[idx], null);
                Interlocked.Decrement(ref _sessionCount);
            }
        }

        private int Wait()
        {
            // could wait on RQ events here
            Thread.SpinWait(1);
            return 0;
        }

        private void Poll()
        {
            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    var sessions = _sessions;
                    var count = 0;
                    var totalCount = 0;
                    var init = _lastIdx;
                    for (int i = _lastIdx; i < sessions.Length; i++)
                    {
                        var session = sessions[i];
                        if (session == null)
                            continue;
                        if ((count = sessions[i].Poll()) > 0)
                        {
                            totalCount += count;
                            _lastIdx = i;
                        }
                    }

                    for (int i = 0; i < init; i++)
                    {
                        var session = sessions[i];
                        if (session == null)
                            continue;
                        if ((count = sessions[i].Poll()) > 0)
                        {
                            totalCount += count;
                            _lastIdx = i;
                        }
                    }

                    if (totalCount == 0)
                        Wait();
                }
                catch (Exception ex)
                {
                    Environment.FailFast("Exception in poller thread: " + ex);
                }
            }
        }

        public void Dispose()
        {
            _thread.Join();
        }
    }
}
