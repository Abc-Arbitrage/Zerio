using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;
using HdrHistogram;

namespace Abc.Zerio.Client
{
    internal static unsafe class Program
    {
        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Console.WriteLine("CLIENT...");

            RunClient(new ZerioClient(new IPEndPoint(IPAddress.Loopback, 48654)));

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunClient(IFeedClient client)
        {
            var histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
            var receivedMessageCount = 0;
            
            void OnMessageReceived(ReadOnlySpan<byte> message)
            {
                var now = Stopwatch.GetTimestamp();
                var start = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(message));
                var rrt = now - start;

                var latencyInMicroseconds = unchecked(rrt * 1_000_000 / (double)Stopwatch.Frequency);

                receivedMessageCount++;

                histogram.RecordValue((long)latencyInMicroseconds);
            }

            using (client)
            {
                var connectedSignal = new AutoResetEvent(false);
                client.Connected += () => Console.WriteLine($"Connected {connectedSignal.Set()}");
                client.MessageReceived += OnMessageReceived;
                client.Start("client");
                connectedSignal.WaitOne();

                do
                {
                    Console.WriteLine("Bench? (<message count> <message size> <delay in micro> <burst>, eg.: 100000 128 10 1)");

                    var benchArgs = Console.ReadLine();

                    if (!TryParseBenchArgs(benchArgs, out var args))
                        break;
                    
                    histogram.Reset();
                    
                    RunBench(client, args, ref receivedMessageCount);

                    histogram.OutputPercentileDistribution(Console.Out, 1);
                    
                } while (true);

                client.Stop();
            }
        }

        private static void RunBench(IFeedClient client, (int messageCount, int messageSize, int delayInMicros, int burstSize) args, ref int receivedMessageCount)
        {
            receivedMessageCount = 0;
            
            Span<byte> message = stackalloc byte[args.messageSize];

            var burstCount = args.messageCount / args.burstSize;
            for (var i = 0; i < burstCount; i++)
            {
                for (var j = 0; j < args.burstSize; j++)
                {
                    Unsafe.WriteUnaligned(ref message[0], Stopwatch.GetTimestamp());
                    client.Send(message);
                }

                BusyWait(args.delayInMicros);
            }
            
            while (receivedMessageCount < burstCount * args.burstSize)
            {
                Thread.Sleep(100);
            }
        }

        private static void BusyWait(double delayInMicros)
        {
            var delayInSeconds = delayInMicros / 1_000_000.0;
            
            if (Math.Abs(delayInSeconds) < 0.000_000_01)
                return;

            var delayInTicks = Math.Round(delayInSeconds * Stopwatch.Frequency);

            var ticks = Stopwatch.GetTimestamp();

            while (Stopwatch.GetTimestamp() - ticks < delayInTicks)
            {
                Thread.SpinWait(1);
            }
        }

        private static bool TryParseBenchArgs(string benchArgs, out (int messageCount, int messageSize, int delayInMicros, int burstSize) args)
        {
            args = default;
            
            var parts = benchArgs.Split(" ");
            if (parts.Length != 4)
                return false;

            if (!int.TryParse(parts[0], out var messageCount))
                return false;
            
            if (!int.TryParse(parts[1], out var messageSize))
                return false;
            
            if (!int.TryParse(parts[2], out var delayInMicros))
                return false;

            if (!int.TryParse(parts[3], out var burstSize))
                return false;

            args = (messageCount, messageSize, delayInMicros, burstSize);
            return true;
        }
    }
}
