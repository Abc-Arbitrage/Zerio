using System;
using System.Diagnostics;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            RunServer(new TcpFeedServer(48654));

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunServer(IFeedServer server)
        {
            using (server)
            {
                var disconnectionSignal = new AutoResetEvent(false);

                server.ClientConnected += peerId => Console.WriteLine("Client connected");
                server.ClientDisconnected += peerId => Console.WriteLine($"Client disconnected {disconnectionSignal.Set()}");

                server.MessageReceived += (peerId, message) => server.Send(peerId, message);

                server.StartAsync("server");

                disconnectionSignal.WaitOne();

                server.Stop();
            }
        }
    }
}
