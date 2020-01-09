using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Core;
using Abc.Zerio.Tcp;
using CsvHelper;
using HdrHistogram;

namespace Abc.Zerio.Client
{
    public class Benchmark
    {
        public const int RIO_PORT = 48654;
        public const int TCP_PORT = 48655;
        public const int ALT_PORT = 48656;

        private readonly int _hostCount;
        private readonly int _serversPerHost;
        private readonly int _clientsPerServer;

        private readonly IFeedClient _feedClientManual;
        private readonly List<IFeedClient> _feedClients = new List<IFeedClient>();
        private const int _warmupSkipCount = 100;
        private readonly string _transportId;
        private readonly string _outFolder;

        private CancellationTokenSource _cts;

        // private LongHistogram _histogram;
        private long _messageCounter;
        private Task _startTask;
        private static readonly Stopwatch _sw = new Stopwatch();

        public Benchmark(IFeedClient feedClientManual, string outFolder = null, string transportId = null)
        {
            _feedClientManual = feedClientManual;

            var osPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "w" : "l";

            _transportId = osPrefix + transportId;

            _outFolder = outFolder ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(_outFolder);

            _messageCounter = 0L;
        }

        public Benchmark(IEnumerable<IFeedClient> feedClients, string outFolder, string transportId, int hostCount, int serversPerHost, int clientsPerServer)
        {
            _hostCount = hostCount;
            _serversPerHost = serversPerHost;
            _clientsPerServer = clientsPerServer;

            _feedClients.AddRange(feedClients);

            var osPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "w" : "l";

            _transportId = osPrefix + transportId;

            _outFolder = outFolder ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(_outFolder);

            _messageCounter = 0L;
        }

        public void Start(int messageSize, int delayMicros = 100, int butst = 1)
        {
            _startTask = Task.Factory.StartNew(() => StartLoop(messageSize, delayMicros, butst));
        }

        public void Stop()
        {
            if (_cts == null || _cts.IsCancellationRequested)
                return;

            _cts.Cancel();
            _startTask.Wait();
            _cts = null;
        }

        private void StartLoop(int messageSize, int delayMicros, int burst)
        {
            _cts = new CancellationTokenSource();
            var histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);

            void FeedClientOnMessageReceived(ReadOnlySpan<byte> bytes)
            {
                var now = Stopwatch.GetTimestamp();

                var count = Interlocked.Increment(ref _messageCounter);
                if (count <= _warmupSkipCount)
                {
                    return;
                }

                var start = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(bytes));
                var rrt = now - start;
                var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);

                if (!_cts.IsCancellationRequested)
                    histogram.RecordValue(micros);
            }

            _feedClientManual.MessageReceived += FeedClientOnMessageReceived;

            histogram.Reset();
            _messageCounter = 0;

            var buffer = new byte[messageSize];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 42;
            }

            var rateReporter = Task.Run(async () =>
            {
                var previousCount = 0L;

                while (!_cts.IsCancellationRequested)
                {
                    var count = Volatile.Read(ref _messageCounter);
                    var countPerSec = count - previousCount;
                    previousCount = count;

                    Console.WriteLine($"Processed messages: {countPerSec:N0}, total: {count:N0} [GC 0:{GC.CollectionCount(0)} 1:{GC.CollectionCount(1)}]");
                    await Task.Delay(1000);
                }
            });

            while (!_cts.IsCancellationRequested)
            {
                _sw.Restart();

                for (int i = 0; i < burst; i++)
                {
                    Unsafe.WriteUnaligned(ref buffer[0], Stopwatch.GetTimestamp());
                    _feedClientManual.Send(buffer);
                }

                NOP(delayMicros / 1000_000.0);
            }

            _feedClientManual.MessageReceived -= FeedClientOnMessageReceived;

            rateReporter.Wait();
            histogram.OutputPercentileDistribution(Console.Out, 1);
            using var writer = new StreamWriter(Path.Combine(_outFolder, $"Latency_{messageSize}_{delayMicros}.hgrm"));
            histogram.OutputPercentileDistribution(writer);
        }

        private volatile bool _isRunning;

        private BenchmarkResult RunTest(int messageSize, int delayMicros, int burst, int tgtMsgCount, int warmUpMessageCount, int secondsPerTest)
        {
            var result = new BenchmarkResult(_feedClients.Count);

            if (_hostCount == 1 && _serversPerHost == 1 && _clientsPerServer == 1)
            {
                result.Transport = _transportId;
            }
            else
            {
                result.Transport = $"{_transportId}_H{_hostCount}_S{_serversPerHost}_C{_clientsPerServer}";
            }

            result.MsgSize = messageSize;
            result.Delay = delayMicros;
            result.Burst = burst;
            result.TgtMsgCount = tgtMsgCount * _feedClients.Count;

            Thread.Sleep(1000);
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            LoopAll(warmUpMessageCount, true);

            GC.Collect(2, GCCollectionMode.Forced, true);

            Thread.Sleep(500);

            result.Started = DateTime.UtcNow;

            var (counter, histogramAll) = LoopAll(tgtMsgCount, false);

            result.ActMsgCount = counter;

            result.Ended = DateTime.UtcNow;

            result.SetStats(histogramAll);

            var name = $"Latency_{_transportId}_{messageSize}_{delayMicros}_{burst}";
            histogramAll.OutputPercentileDistribution(Console.Out, 1);
            using var writer = new StreamWriter(Path.Combine(_outFolder, $"Latency_{_transportId}_{messageSize}_{delayMicros}_{burst}.hgrm"));
            histogramAll.OutputPercentileDistribution(writer);
            Console.WriteLine(name);
            Console.WriteLine($"TgtMsgSec: {result.TgtMsgSec:N0}, ActMsgSec: {result.ActMsgSec:N0}, TgtBw: {result.TgtBwMb:N0} Mb, ActBw: {result.ActBwMb:N0} Mb");
            Console.WriteLine("------------------------------");
            return result;

            (int, LongHistogram) LoopAll(int msgCount, bool warmup)
            {
                _isRunning = true;
                var histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);

                Task[] threads = new Task[_feedClients.Count];
                (MessageCounter, LongHistogram)[] results = new (MessageCounter, LongHistogram)[_feedClients.Count];

                for (int i = 0; i < _feedClients.Count; i++)
                {
                    var i1 = i;
                    threads[i] = Task.Factory.StartNew(() =>
                                                       {
                                                           try
                                                           {
                                                               results[i1] = Loop(_feedClients[i1], msgCount, warmup);
                                                           }
                                                           catch (Exception ex)
                                                           {
                                                               Console.Error.WriteLine($"Error in test run task: {ex}");
                                                               throw;
                                                           }
                                                       },
                                                       TaskCreationOptions.LongRunning);
                }

                try
                {
                    Task.WaitAll(threads);
                }
                catch
                {
                    _isRunning = false;
                    throw;
                }

                foreach (var (_, h) in results)
                {
                    if (h != null)
                        histogram.Add(h);
                }

                _isRunning = false;

                return (results.Sum(r => r.Item1?.Count ?? 0), histogram);
            }

            (MessageCounter, LongHistogram) Loop(IFeedClient client, int msgCountX, bool warmup)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(messageSize);

                var histogram = new LongHistogram(TimeSpan.FromSeconds(10).Ticks, 2);
                var msgCounter = new MessageCounter();

                var burstsCount = msgCountX / burst; // int division, could lose remainder
                var tgtMsgCounter = new MessageCounter(burstsCount * burst); // adjust for remainder

                var mre = new ManualResetEventSlim(false);

                client.MessageReceived += Handler;

                // msgCount is total, including bursts

                var waitTask = Task.Run(() => { WaitMsgCounter(mre); });

                // Send loop

                var sentCount = 0;

                for (int i = 0; i < burstsCount && _isRunning; i++)
                {
                    _sw.Restart();

                    for (int b = 0; b < burst && _isRunning; b++)
                    {
                        Unsafe.WriteUnaligned(ref buffer[0], Stopwatch.GetTimestamp());
                        client.Send(buffer);
                        sentCount++;
                    }

                    NOP(delayMicros / 1000_000.0);
                }

                tgtMsgCounter.Count = sentCount; // if _isRunning was sent to false during send loop

                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Done sending, waiting for receiving all messages back (warmup={warmup})");

                waitTask.Wait();

                client.MessageReceived -= Handler;

                ArrayPool<byte>.Shared.Return(buffer);

                return (msgCounter, histogram);

                void Handler(ReadOnlySpan<byte> bytes)
                {
                    var now = Stopwatch.GetTimestamp();
                    var start = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(bytes));
                    var rrt = now - start;
                    var micros = (long)(rrt * 1_000_000.0 / Stopwatch.Frequency);

                    if (!warmup)
                    {
                        try
                        {
                            histogram.RecordValue(micros);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    if (msgCounter.Increment() >= tgtMsgCounter.Count)
                    {
                        // _log.Info($"[{Thread.CurrentThread.ManagedThreadId}] Done receiving normally msgCounter.Count [{msgCounter.Count}] >= tgtMsgCounter.Count [{tgtMsgCounter.Count}] (warmup={warmup})");
                        mre.Set();
                    }
                }

                void WaitMsgCounter(ManualResetEventSlim manualReset)
                {
                    // Need to wait for all NAKed data, otherwise the next test suffers from resent data
                    for (int i = 0; i < 50; i++)
                    {
                        if (!manualReset.Wait(secondsPerTest * 1000 * 2))
                        {
                            if (msgCounter.Count >= tgtMsgCounter.Count)
                            {
                                Console.WriteLine("Exiting WaitMsgCounter: msgCounter.Count >= tgtMsgCounter.Count");
                                // edge case when we stopped sending and adjusted target message count but the handler has already processed all messages
                                manualReset.Set();
                                return;
                            }

                            Console.WriteLine($"Exceeded {2 + i * 2}x time of secondsPerTest [{secondsPerTest}] with {msgCounter.Count} messages of {tgtMsgCounter.Count}  for {_transportId}_{messageSize}_{delayMicros}_{burst}  (warmup={warmup})");
                            _isRunning = false;
                        }
                        else
                        {
                            return;
                        }
                    }

                    throw new Exception($"Exited test with {msgCounter.Count} messages of {tgtMsgCounter.Count} for {_transportId}_{messageSize}_{delayMicros}_{burst}  (warmup={warmup})");
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        private static void NOP(double durationSeconds)
        {
            if (Math.Abs(durationSeconds) < 0.000_000_01)
            {
                return;
            }

            var durationTicks = Math.Round(durationSeconds * Stopwatch.Frequency);

            while (_sw.ElapsedTicks < durationTicks)
            {
                Thread.SpinWait(1);
            }
        }

        public static void RunAll(string transportType, List<string> hosts, int secondsPerTest = 5, int maxBandwidthMb = 100, string outFolder = null, int serverCount = 1, int clientsPerServer = 1, string suffix = "")
        {
            var dateString = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            outFolder ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks", dateString);
            Directory.CreateDirectory(outFolder);

            List<BenchmarkResult> results = new List<BenchmarkResult>();

            switch (transportType.ToLowerInvariant())
            {
                case "t":
                case "tcp":
                {
                    var tcpClients = new List<TcpFeedClient>();
                    foreach (var host in hosts)
                    {
                        for (int i = 0; i < serverCount * clientsPerServer; i++)
                        {
                            var portDelta = i % serverCount;
                            var tcpClient = new TcpFeedClient(new IPEndPoint(IPAddress.Parse(host), TCP_PORT + portDelta));

                            var connectedMre = new ManualResetEvent(false);
                            tcpClient.Connected += () => { connectedMre.Set(); };
                            tcpClient.Start($"tcp_client_{i}");
                            if (!connectedMre.WaitOne(1000))
                            {
                                Console.WriteLine("Cannot connect");
                                return;
                            }

                            tcpClients.Add(tcpClient);
                        }
                    }

                    var code = string.IsNullOrWhiteSpace(suffix) ? "tcp" : "tcp" + "_" + suffix;
                    results.AddRange(RunWithClients(tcpClients, code, secondsPerTest, maxBandwidthMb, outFolder, hosts.Count, serverCount, clientsPerServer));

                    foreach (var tcpClient in tcpClients)
                    {
                        tcpClient.Dispose();
                    }
                }
                    break;

                case "r":
                case "rio":
                {
                    var rioClients = new List<ZerioClient>();
                    foreach (var host in hosts)
                    {
                        for (int i = 0; i < serverCount * clientsPerServer; i++)
                        {
                            var portDelta = i % serverCount;
                            var tcpClient = new ZerioClient(new IPEndPoint(IPAddress.Parse(host), RIO_PORT + portDelta));

                            var connectedMre = new ManualResetEvent(false);
                            tcpClient.Connected += () => { connectedMre.Set(); };
                            tcpClient.Start($"rio_client_{i}");
                            if (!connectedMre.WaitOne(1000))
                            {
                                Console.WriteLine("Cannot connect");
                                return;
                            }

                            rioClients.Add(tcpClient);
                        }
                    }

                    var code = string.IsNullOrWhiteSpace(suffix) ? "rio" : "rio" + "_" + suffix;
                    results.AddRange(RunWithClients(rioClients, code, secondsPerTest, maxBandwidthMb, outFolder, hosts.Count, serverCount, clientsPerServer));

                    foreach (var rioClient in rioClients)
                    {
                        rioClient.Dispose();
                    }
                }
                    break;

                case "a":
                case "alt":
                {
                    var rioClients = new List<Alt.ZerioClient>();
                    foreach (var host in hosts)
                    {
                        for (int i = 0; i < serverCount * clientsPerServer; i++)
                        {
                            var portDelta = i % serverCount;
                            var tcpClient = new Alt.ZerioClient(new IPEndPoint(IPAddress.Parse(host), ALT_PORT + portDelta));

                            var connectedMre = new ManualResetEvent(false);
                            tcpClient.Connected += () => { connectedMre.Set(); };
                            tcpClient.Start($"alt_client_{i}");
                            if (!connectedMre.WaitOne(1000))
                            {
                                Console.WriteLine("Cannot connect");
                                return;
                            }

                            rioClients.Add(tcpClient);
                        }
                    }

                    var code = string.IsNullOrWhiteSpace(suffix) ? "alt" : "alt" + "_" + suffix;
                    results.AddRange(RunWithClients(rioClients, code, secondsPerTest, maxBandwidthMb, outFolder, hosts.Count, serverCount, clientsPerServer));

                    foreach (var rioClient in rioClients)
                    {
                        rioClient.Dispose();
                    }
                }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown transport type: {transportType}");
            }

            var fileName = $"Stats_{dateString}.csv";

            using (var writer = new StreamWriter(Path.Combine(outFolder, fileName)))
            using (var csv = new CsvWriter(writer))
            {
                csv.WriteRecords(results);
            }

            using (var csvWriter = new CsvWriter(Console.Out))
            {
                csvWriter.WriteRecords(results);
                Console.Out.Flush();
            }

            Console.WriteLine("------------------------------");
            Console.WriteLine("RunAll finished");
        }

        private static IEnumerable<BenchmarkResult> RunLoadWithClients(IEnumerable<IFeedClient> clients, string transportId, string outFolder, int hostCount)
        {
            // this generates ~200 Mbps load on each host
            // code copied from RunWithClients, secondsPerTest set to very big value

            var msgSizes = new[] { 512 };
            var delays = new[] { 100 };
            var bursts = new[] { 5 };

            var transportFeedClients = clients.ToList();

            var benchmark = new Benchmark(transportFeedClients, outFolder, transportId, hostCount, 1, 1);

            var maxBandwidthMb = 5000;
            var secondsPerTest = 60 * 60 * 5; // 5 hours, need to stop manually

            foreach (var msgSize in msgSizes)
            {
                foreach (var delay in delays)
                {
                    foreach (var burst in bursts)
                    {
                        if (burst >= 10 && delay < 100)
                        {
                            continue;
                        }

                        // we want this number of msg/sec
                        var tgtMsgSec = (1_000_000.0 / delay) * burst;

                        // which gives this bw
                        var tgtBw = transportFeedClients.Count * tgtMsgSec * msgSize * 8 / 1_000_000.0;

                        // but if tgt bw is above link capacity we could wait more than secondsPerTest
                        // so we want to limit the number of messages to fit into secondsPerTest
                        // we will send as aggressive as without this adjustment, but less number of messages
                        tgtMsgSec = (1.0 / transportFeedClients.Count) * Math.Min(tgtBw, maxBandwidthMb * 0.8) * 1_000_000.0 / (msgSize * 8);

                        var tgtMsgCount = (int)(tgtMsgSec * secondsPerTest);

                        if (tgtMsgCount < 1000)
                        {
                            continue;
                        }

                        // warmup one sec
                        var warmUpMessageCount = Math.Max((int)tgtMsgSec, 100);
                        BenchmarkResult result;

                        while (true)
                        {
                            try
                            {
                                result = benchmark.RunTest(msgSize, delay, burst, tgtMsgCount, warmUpMessageCount, secondsPerTest);
                                break;
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText(Path.Combine(benchmark._outFolder, $"Failed_{transportId}_{msgSize}_{delay}_{burst}.log"), $"{DateTime.UtcNow}: {ex}");
                                Console.WriteLine($"Failed at {transportId}_{msgSize}_{delay}_{burst}, trying again...");
                                Thread.Sleep(1000);
                            }
                        }

                        if (result != null)
                            yield return result;
                    }
                }
            }
        }

        private static IEnumerable<BenchmarkResult> RunWithClients(IEnumerable<IFeedClient> clients,
                                                                   string transportId,
                                                                   int secondsPerTest,
                                                                   int maxBandwidthMb,
                                                                   string outFolder,
                                                                   int hostCount,
                                                                   int serverCount,
                                                                   int clientsPerServer)
        {
            var msgSizes = new[] { 1024, 32, 512, 128 }; //  2500, , 128, 32 
            var delays = new[] { 10, 40, 100, 1000 }; // 20,10,, 2000
            var bursts = new[] { 50, 5, 2, 1 }; // 50,

            var transportFeedClients = clients.ToList();

            var benchmark = new Benchmark(transportFeedClients, outFolder, transportId, hostCount, serverCount, clientsPerServer);

            foreach (var msgSize in msgSizes)
            {
                foreach (var delay in delays)
                {
                    foreach (var burst in bursts)
                    {
                        if (burst >= 10 && delay < 100)
                        {
                            continue;
                        }

                        // we want this number of msg/sec
                        var tgtMsgSec = (1_000_000.0 / delay) * burst;

                        // which gives this bw
                        var tgtBw = transportFeedClients.Count * tgtMsgSec * msgSize * 8 / 1_000_000.0;

                        // but if tgt bw is above link capacity we could wait more than secondsPerTest
                        // so we want to limit the number of messages to fit into secondsPerTest
                        // we will send as aggressive as without this adjustment, but less number of messages
                        tgtMsgSec = (1.0 / transportFeedClients.Count) * Math.Min(tgtBw, maxBandwidthMb * 0.8) * 1_000_000.0 / (msgSize * 8);

                        var tgtMsgCount = (int)(tgtMsgSec * secondsPerTest);

                        if (tgtMsgCount < 1000)
                        {
                            continue;
                        }

                        // warmup one sec
                        var warmUpMessageCount = Math.Max((int)tgtMsgSec, 100);
                        BenchmarkResult result;

                        while (true)
                        {
                            try
                            {
                                result = benchmark.RunTest(msgSize, delay, burst, tgtMsgCount, warmUpMessageCount, secondsPerTest);
                                break;
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText(Path.Combine(benchmark._outFolder, $"Failed_{transportId}_{msgSize}_{delay}_{burst}.log"), $"{DateTime.UtcNow}: {ex}");
                                Console.WriteLine($"Failed at {transportId}_{msgSize}_{delay}_{burst}, trying again...");
                                Thread.Sleep(1000);
                            }
                        }

                        if (result != null)
                            yield return result;
                    }
                }
            }
        }

        class MessageCounter
        {
            public volatile int Count;

            public MessageCounter()
            {
            }

            public MessageCounter(int count)
            {
                Count = count;
            }

            public int Increment()
            {
                return Interlocked.Increment(ref Count);
            }
        }
    }
}
