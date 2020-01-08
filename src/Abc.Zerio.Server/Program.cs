using System;
using System.Diagnostics;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;

namespace Abc.Zerio.Server
{
    public static class Program
    {
        public const int RIO_PORT = 48654;
        public const int TCP_PORT = 48655;
        public const int ALT_PORT = 48656;

        private static long _messageCounter;

        private static void Main()
        {
            Console.BackgroundColor = ConsoleColor.DarkRed;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            //using var rioServer = new ZerioServer(RIO_PORT);
            //using var tcpServer = new TcpFeedServer(TCP_PORT);
            using var altServer = new Alt.ZerioServer(ALT_PORT);

            //StartServer(rioServer);
            //StartServer(tcpServer);
            StartServer(altServer);

            const bool alwaysPrintStats = false;
            var cts = new CancellationTokenSource();

            var monitoringThread = new Thread(() =>
            {
                var previousCount = 0L;
                var previousGC0Count = 0L;
                var previousGC1Count = 0L;

                while (!cts.IsCancellationRequested)
                {
                    var count = Volatile.Read(ref _messageCounter);
                    var countPerSec = count - previousCount;
                    previousCount = count;

                    var gc0Count = GC.CollectionCount(0);
                    var gc0countPerSec = gc0Count - previousGC0Count;
                    previousGC0Count = gc0Count;

                    var gc1Count = GC.CollectionCount(1);
                    var gc1countPerSec = gc1Count - previousGC1Count;
                    previousGC1Count = gc1Count;

                    if (alwaysPrintStats || gc0countPerSec != 0 || gc1countPerSec != 0)
                    {
                        // alloc
                        Console.WriteLine($"Processed messages: {countPerSec:N0}, total: {count:N0} [GC 0:{gc0countPerSec} 1:{gc1countPerSec}]");
                    }

                    Thread.Sleep(1_000);
                }
            });

            monitoringThread.Start();

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();

            cts.Cancel();
            monitoringThread.Join();
            //rioServer.Stop();
            //tcpServer.Stop();
            altServer.Stop();
        }

        private static void StartServer(IFeedServer server)
        {
            server.ClientConnected += peerId => Console.WriteLine($"Client '{peerId}' connected.");
            server.ClientDisconnected += peerId => Console.WriteLine($"Client '{peerId}' disconnected. ");
            server.MessageReceived += (s, span) =>
            {
                server.Send(s, span);
                Interlocked.Increment(ref _messageCounter);
            };
            server.Start(server.GetType().Name);
        }
    }
}
