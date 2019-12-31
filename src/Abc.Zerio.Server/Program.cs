using System;
using System.Diagnostics;
using System.Threading;
using Abc.Zerio.Core;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            RunServer(new ZerioServer(48654));

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunServer(IFeedServer server)
        {
            using (server)
            {
                var disconnectionSignal = new AutoResetEvent(false);

                server.ClientConnected += peerId => Console.WriteLine($"Client '{peerId}' connected.");
                server.ClientDisconnected += peerId => Console.WriteLine($"Client '{peerId}' disconnected. " + disconnectionSignal.Set());
                server.MessageReceived += server.Send;

                server.StartAsync("server");

                disconnectionSignal.WaitOne();

                server.Stop();
            }
        }
    }
}
