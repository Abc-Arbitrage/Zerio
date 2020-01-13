using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Threading;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;
using NDesk.Options;

namespace Abc.Zerio.Client
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;

            var proc = Process.GetCurrentProcess();

            // Just in case if something allocates
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            Console.WriteLine("IsServerGC: " + GCSettings.IsServerGC);
            Console.WriteLine("Latency mode: " + GCSettings.LatencyMode);

            var showHelp = false;

            var hosts = new List<string>();

            decimal spt = 2;
            decimal bw = 100;
            var runAll = false;
            var transport = string.Empty;
            var highPriority = true;
            var clientsPerServer = 1;
            var suffix = string.Empty;
            const int serverCount = 1;

            var p = new OptionSet
            {
                { "h|host=", "hostname", v => hosts.Add(v) },
                { "a|all", "run all test", v => runAll = true },
                { "t|transport=", "transport to use for manual test", v => transport = v },
                { "p|priority", "run with high priority", v => highPriority = true },
                { "spt|secondsPerTest=", "how many seconds a single test should take", v => spt = int.Parse(v) },
                { "bw|bandwidth=", "bandwidths limit in Mbps", v => bw = decimal.Parse(v) },
                { "c|clients=", "number of local clients per server", v => clientsPerServer = (int)decimal.Parse(v) },
                { "x|suffix=", "transport suffix", v => suffix = v },
                { "help", "show this message and exit", v => showHelp = v != null },
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try --help for more information.");
                return;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return;
            }

            if (string.IsNullOrEmpty(transport))
            {
                Console.ReadLine();
            }

            if (hosts.Count == 0)
            {
                hosts.Add(GetLocalIPAddress());
            }

            if (highPriority)
            {
                proc.PriorityClass = ProcessPriorityClass.High;
            }

            if (runAll)
            {
                Benchmark.RunAll(transport, hosts, (int)spt, (int)bw, null, serverCount, clientsPerServer, suffix);
            }
            else
            {
                using var client = CreateClient(hosts[0], transport);
                ManualBenchmark(client);
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = host.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(x => x.ToString()).FirstOrDefault();
            return ipAddress ?? string.Empty;
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static IFeedClient CreateClient(string hostname, string transportType)
        {
            switch (transportType.ToLowerInvariant())
            {
                case "t":
                case "tcp":
                    return new TcpFeedClient(new IPEndPoint(Dns.GetHostAddresses(hostname).First(i => i.AddressFamily == AddressFamily.InterNetwork), Benchmark.TCP_PORT));

                case "r":
                case "rio":
                    return new ZerioClient(new IPEndPoint(Dns.GetHostAddresses(hostname).First(i => i.AddressFamily == AddressFamily.InterNetwork), Benchmark.RIO_PORT));

                case "a":
                case "alt":
                    return new Alt.ZerioClient(new IPEndPoint(Dns.GetHostAddresses(hostname).First(i => i.AddressFamily == AddressFamily.InterNetwork), Benchmark.ALT_PORT));

                default:
                    throw new InvalidOperationException($"Unknown transport type: {transportType}");
            }
        }

        static void ManualBenchmark(IFeedClient client)
        {
            var connectedMre = new ManualResetEvent(false);

            client.Connected += () => { connectedMre.Set(); };

            Thread.Sleep(3000);

            client.Start($"{client.GetType().Name}");

            if (!connectedMre.WaitOne(1000))
            {
                Console.WriteLine("Cannot connect");
                return;
            }

            var benchmark = new Benchmark(client);

            int size;
            int delay;
            int burst = 1;
            while (true)
            {
                while (true)
                {
                    try
                    {
                        Console.WriteLine("Enter size in bytes, delay in micros, and (optionally) burst , e.g. 512 100 2");

                        var sizeDelay = Console.ReadLine();
                        if (string.IsNullOrEmpty(sizeDelay) || sizeDelay == "q")
                        {
                            client.Dispose();
                            return;
                        }

                        var parts = sizeDelay.Split(' ');
                        size = int.Parse(parts[0]);
                        delay = int.Parse(parts[1]);
                        if (parts.Length > 2)
                        {
                            burst = int.Parse(parts[2]);
                        }

                        break;
                    }
                    catch
                    {
                    }
                }

                benchmark.Start(size, delay, burst);
                Console.ReadLine();
                benchmark.Stop();
            }
        }
    }
}
