using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;

namespace Abc.Zerio.Client
{
    internal static unsafe class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("CLIENT...");

            RunClient(new TcpFeedClient(new IPEndPoint(IPAddress.Loopback, 48654)));

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunClient(IFeedClient client)
        {
            using (client)
            {
                var connectedSignal = new AutoResetEvent(false);
                client.Connected += () => Console.WriteLine($"Connected {connectedSignal.Set()}");
                client.MessageReceived += OnMessageReceived;
                client.StartAsync("client");

                connectedSignal.WaitOne();
                Span<byte> message = stackalloc byte[64];

                for (var i = 0; i < 10000; i++)
                {
                    Unsafe.WriteUnaligned(ref message[0], Stopwatch.GetTimestamp());
                    client.Send(message);
                    Thread.Sleep(100);
                }

                client.Stop();
            }
        }

        private static void OnMessageReceived(ReadOnlySpan<byte> message)
        {
            var now = Stopwatch.GetTimestamp();
            var start = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(message));
            var rrt = now - start;

            var latencyInMicroseconds = unchecked(rrt * 1_000_000 / (double)Stopwatch.Frequency);
            Console.WriteLine($"Message echoed in {latencyInMicroseconds:N0} µs");
        }
    }
}
