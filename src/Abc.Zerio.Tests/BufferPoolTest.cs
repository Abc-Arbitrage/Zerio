using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Core;
using Abc.Zerio.Interop;
using NUnit.Framework;

namespace Abc.Zerio.Tests
{
    [TestFixture]
    public unsafe class BufferPoolTest
    {
        [StructLayout(LayoutKind.Sequential, Size = 32)]
        public struct TestStruct
        {
        }

        [Test, Explicit("Long running")]
        public void ConcurrentBagTest()
        {
            int iterations = 100_000_000;
            int count = 0;
            var capacity = 64 * 1024;
            var cb1 = new ConcurrentBag<TestStruct>();
            var cb2 = new ConcurrentBag<TestStruct>();
            for (int i = 0; i < capacity; i++)
            {
                cb1.Add(new TestStruct());
                cb2.Add(new TestStruct());
            }

            for (int i = 0; i < capacity; i++)
            {
                cb2.TryTake(out _);
            }

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            for (int i = 0; i < 16; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                                                {
                                                    while (true)
                                                    {
                                                        if (Interlocked.Increment(ref count) > iterations)
                                                        {
                                                            return;
                                                        }

                                                        if (cb2.TryTake(out var ts))
                                                        {
                                                            cb1.Add(ts);
                                                        }
                                                        
                                                        if (cb1.TryTake(out ts))
                                                        {
                                                            cb2.Add(ts);
                                                        }
                                                    }
                                                },
                                                TaskCreationOptions.LongRunning));
            }

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = iterations / seconds / 1_000_000;
            Console.WriteLine($"Count: {count}");
            Console.WriteLine($"MOPS: {2 * mops:N2}");
        }


        [Test, Explicit("Long running")]
        public void ConcurrentQueueTest()
        {
            int iterations = 100_000_000;
            int count = 0;
            var capacity = 64 * 1024;
            var cb1 = new ConcurrentQueue<TestStruct>();
            var cb2 = new ConcurrentQueue<TestStruct>();
            for (int i = 0; i < capacity; i++)
            {
                cb1.Enqueue(new TestStruct());
                cb2.Enqueue(new TestStruct());
            }

            for (int i = 0; i < capacity; i++)
            {
                cb2.TryDequeue(out _);
            }

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            for (int i = 0; i < 16; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        if (Interlocked.Increment(ref count) > iterations)
                        {
                            return;
                        }

                        if (cb2.TryDequeue(out var ts))
                        {
                            cb1.Enqueue(ts);
                        }

                        if (cb1.TryDequeue(out ts))
                        {
                            cb2.Enqueue(ts);
                        }
                    }
                },
                                                TaskCreationOptions.LongRunning));
            }

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = iterations / seconds / 1_000_000;
            Console.WriteLine($"Count: {count}");
            Console.WriteLine($"MOPS: {2 * mops:N2}");
        }

        [Test, Explicit("Long running")]
        public void ConcurrentStackTest()
        {
            int iterations = 100_000_000;
            int count = 0;
            var capacity = 64 * 1024;
            var cb1 = new ConcurrentStack<TestStruct>();
            var cb2 = new ConcurrentStack<TestStruct>();
            for (int i = 0; i < capacity; i++)
            {
                cb1.Push(new TestStruct());
                cb2.Push(new TestStruct());
            }

            for (int i = 0; i < capacity; i++)
            {
                cb2.TryPop(out _);
            }

            List<Task> tasks = new List<Task>();
            var ticks = Stopwatch.GetTimestamp();
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");

            for (int i = 0; i < 16; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        if (Interlocked.Increment(ref count) > iterations)
                        {
                            return;
                        }

                        if (cb2.TryPop(out var ts))
                        {
                            cb1.Push(ts);
                        }

                        if (cb1.TryPop(out ts))
                        {
                            cb2.Push(ts);
                        }
                    }
                },
                                                TaskCreationOptions.LongRunning));
            }

            Task.WhenAll(tasks).Wait();
            ticks = Stopwatch.GetTimestamp() - ticks;
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            var seconds = ticks * 1.0 / Stopwatch.Frequency;
            var mops = iterations / seconds / 1_000_000;
            Console.WriteLine($"Count: {count}");
            Console.WriteLine($"MOPS: {2 * mops:N2}");
        }
    }
}
