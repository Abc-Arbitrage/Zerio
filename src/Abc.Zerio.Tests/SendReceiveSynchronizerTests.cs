using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Alt;
using Abc.Zerio.Alt.Buffers;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class SendReceiveSynchronizerTests
    {
        internal interface IRioSendReceive
        {
            void Send(RioSegment segment);
            void Receive(RioSegment segment);
        }

        internal struct SendReceiveSynchronizer<T>
            where T : IRioSendReceive
        {
            public T SendReceive;

            private long _head;
            private long _tail;
            private long _lastProcessed;
            private int _lock;
            private const int _queueLength = 64;
            private const int _idxMask = _queueLength - 1;

            // long to keep segment alignment to 8
            private readonly (RioSegment segment, long isSend)[] _requests;

            internal long RaceCounter;

            public SendReceiveSynchronizer(T sendReceive)
            {
                SendReceive = sendReceive;
                _requests = new (RioSegment segment, long isSend)[_queueLength];
                _head = 0;
                _tail = 0;
                _lastProcessed = 0;
                _lock = 0;
                RaceCounter = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Send(RioSegment segment)
            {
                var head = Interlocked.Increment(ref _head);
                var idx = head & _idxMask;
                _requests[idx] = (segment, 1);
                Process(Interlocked.Increment(ref _tail));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Receive(RioSegment segment)
            {
                var head = Interlocked.Increment(ref _head);
                var idx = head & _idxMask;
                _requests[idx] = (segment, 0);
                Process(Interlocked.Increment(ref _tail));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(long tail)
            {
                if (tail <= Volatile.Read(ref _lastProcessed))
                {
                    Interlocked.Increment(ref RaceCounter);
                    return;
                }

                while (true)
                {
                    if (0 == Interlocked.CompareExchange(ref _lock, 1, 0))
                    {
                        while (true)
                        {
                            if (Volatile.Read(ref _lastProcessed) >= Volatile.Read(ref _tail))
                                break;

                            if (Volatile.Read(ref _tail) < Volatile.Read(ref _head))
                            {
                                Thread.SpinWait(1);
                                continue;
                            }

                            //if (Volatile.Read(ref _lastProcessed) == Volatile.Read(ref _tail)
                            //&& Volatile.Read(ref _head) == Volatile.Read(ref _tail))
                            //break;

                            var idx = (Interlocked.Increment(ref _lastProcessed) - 1) & _idxMask;
                            var item = _requests[idx];

                            if (item.isSend != 0)
                            {
                                SendReceive.Send(item.segment);
                            }
                            else
                            {
                                SendReceive.Receive(item.segment);
                            }
                        }

                        Volatile.Write(ref _lock, 0);
                        break;
                    }

                    Thread.SpinWait(1);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct TestSendReceive : IRioSendReceive
        {
            public long Count;

            public void Send(RioSegment segment)
            {
                Count++;
            }

            public void Receive(RioSegment segment)
            {
                Count++;
            }
        }

        [Test, Explicit("Long running")]
        public void SynchronizerTest()
        {
            int iterations = 1_000_000;
            var synchonizer = new SendReceiveSynchronizer<TestSendReceive>(new TestSendReceive());

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    synchonizer.Send(default);
                                                }

                                                Console.WriteLine("Finished send");
                                            },
                                            TaskCreationOptions.LongRunning));

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    synchonizer.Receive(default);
                                                }

                                                Console.WriteLine("Finished receive");
                                            },
                                            TaskCreationOptions.LongRunning));

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;

            Assert.AreEqual(2 * iterations, synchonizer.SendReceive.Count);

            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = 2 * iterations / seconds / 1_000_000;
            Console.WriteLine($"MOPS: {mops:N2}");
            Console.WriteLine($"Races: {synchonizer.RaceCounter:N0}");
        }

        [Test, Explicit("Long running")]
        public void LockTest()
        {
            int iterations = 100_000_000;
            var sr = new TestSendReceive();
            var locker = new object();

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    lock (locker)
                                                    {
                                                        sr.Send(default);
                                                    }
                                                }
                                            },
                                            TaskCreationOptions.LongRunning));

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    lock (locker)
                                                    {
                                                        sr.Receive(default);
                                                    }
                                                }
                                            },
                                            TaskCreationOptions.LongRunning));

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;

            Assert.AreEqual(2 * iterations, sr.Count);

            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = 2 * iterations / seconds / 1_000_000;
            Console.WriteLine($"MOPS: {mops:N2}");
        }

        [Test, Explicit("Long running")]
        public void CASTest()
        {
            int iterations = 100_000_000;
            var sr = new TestSendReceive();
            var sl = new SpinLock(false);

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    var taken = false;
                                                    sl.Enter(ref taken);
                                                    if (!taken)
                                                        throw new InvalidOperationException();

                                                    sr.Send(default);

                                                    sl.Exit();
                                                }
                                            },
                                            TaskCreationOptions.LongRunning));

            tasks.Add(Task.Factory.StartNew(() =>
                                            {
                                                for (int i = 0; i < iterations; i++)
                                                {
                                                    var taken = false;
                                                    sl.Enter(ref taken);
                                                    if (!taken)
                                                        throw new InvalidOperationException();

                                                    sr.Receive(default);

                                                    sl.Exit();
                                                }
                                            },
                                            TaskCreationOptions.LongRunning));

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;

            Assert.AreEqual(2 * iterations, sr.Count);

            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = 2 * iterations / seconds / 1_000_000;
            Console.WriteLine($"MOPS: {mops:N2}");
        }
    }
}
