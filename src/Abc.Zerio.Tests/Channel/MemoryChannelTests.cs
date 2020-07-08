using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zerio.Channel;
using NUnit.Framework;

namespace Abc.Zerio.Tests.Channel
{
    [TestFixture]
    public class MemoryChannelTests
    {
        [Test]
        public void Should_write_from_multiple_threads()
        {
            // Arrange
            const int taskCount = 5;
            const int messageCountPerTask = 100_000;
            var countdownSignal = new CountdownEvent(taskCount * messageCountPerTask);

            var receivedMessages = new List<string>();

            var memoryChannel = new RegisteredMemoryChannel();
            memoryChannel.MessageReceived +=  (messageBytes, endOfBatch, cleanupNeeded)  =>
            {
                receivedMessages.Add(Encoding.ASCII.GetString(messageBytes.ToArray()));
                countdownSignal.Signal();
                
                if(cleanupNeeded)
                    memoryChannel.CleanupPartitions();
            };

            memoryChannel.Start(false);

            // Act
            var publishingTasks = Enumerable.Range(0, taskCount).Select(x => Task.Run(() =>
            {
                for (var i = 0; i < messageCountPerTask; i++)
                {
                    var messageBytes = Encoding.ASCII.GetBytes($"T{x};M{i}");
                    memoryChannel.Send(messageBytes.AsSpan());
                }
            }));

            Task.WaitAll(publishingTasks.ToArray());

            // Assert
            countdownSignal.Wait(TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(receivedMessages.Count, taskCount * messageCountPerTask);

            for (var i = 0; i < taskCount; i++)
            {
                var taskMessages = receivedMessages.Where(x => x.StartsWith($"T{i}")).ToArray();
                Assert.AreEqual(taskMessages.Length, messageCountPerTask);

                for (var j = 0; j < messageCountPerTask; j++)
                {
                    Assert.AreEqual(taskMessages[j], $"T{i};M{j}");
                }
            }

            memoryChannel.Stop();
        }

        [Test]
        public void Should_be_manually_polled()
        {
            // Arrange
            const int taskCount = 5;
            const int messageCountPerTask = 100_000;
            var countdownSignal = new CountdownEvent(taskCount * messageCountPerTask);

            var receivedMessages = new List<string>();

            var memoryChannel = new RegisteredMemoryChannel();
            memoryChannel.MessageReceived += (messageBytes, endOfBatch, cleanupNeeded) =>
            {
                receivedMessages.Add(Encoding.ASCII.GetString(messageBytes.ToArray()));
                countdownSignal.Signal();
                
                if(cleanupNeeded)
                    memoryChannel.CleanupPartitions();
            };

            memoryChannel.Start(true);

            Task.Run(() =>
            {
                while (receivedMessages.Count < taskCount * messageCountPerTask)
                    memoryChannel.TryPoll();
            });

            // Act
            var publishingTasks = Enumerable.Range(0, taskCount).Select(x => Task.Run(() =>
            {
                for (var i = 0; i < messageCountPerTask; i++)
                {
                    var messageBytes = Encoding.ASCII.GetBytes($"T{x};M{i}");
                    memoryChannel.Send(messageBytes.AsSpan());
                }
            }));

            Task.WaitAll(publishingTasks.ToArray());

            // Assert
            countdownSignal.Wait(TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(receivedMessages.Count, taskCount * messageCountPerTask);

            for (var i = 0; i < taskCount; i++)
            {
                var taskMessages = receivedMessages.Where(x => x.StartsWith($"T{i}")).ToArray();
                Assert.AreEqual(taskMessages.Length, messageCountPerTask);

                for (var j = 0; j < messageCountPerTask; j++)
                {
                    Assert.AreEqual(taskMessages[j], $"T{i};M{j}");
                }
            }

            memoryChannel.Stop();
        }

        [Test]
        public void Should_manually_poll_multiple_channels()
        {
            // Arrange
            const int taskCount = 5;
            const int messageCountPerTask = 100_000;
            var countdownSignal = new CountdownEvent(taskCount * messageCountPerTask);

            var receivedMessages = new List<string>();

            void OnMessageReceived(RegisteredMemoryChannel memoryChannel, ReadOnlySpan<byte> messageBytes, bool endOfBatch, bool cleanupNeeded) 
            {
                receivedMessages.Add(Encoding.ASCII.GetString(messageBytes.ToArray()));
                countdownSignal.Signal();
                
                if(cleanupNeeded)
                    memoryChannel.CleanupPartitions();
            }

            var memoryChannels = new List<RegisteredMemoryChannel>();
            var publishingTasks = new List<Task>();

            for (var i = 0; i < taskCount; i++)
            {
                var memoryChannel = new RegisteredMemoryChannel();
                memoryChannel.MessageReceived += (messageBytes, batch, cleanupNeeded) =>  OnMessageReceived(memoryChannel, messageBytes, batch, cleanupNeeded);
                memoryChannel.Start(true);
                memoryChannels.Add(memoryChannel);

                var taskId = i;
                var publishingTask = new Task(() =>
                {
                    for (var j = 0; j < messageCountPerTask; j++)
                    {
                        var messageBytes = Encoding.ASCII.GetBytes($"T{taskId};M{j}");
                        memoryChannel.Send(messageBytes.AsSpan());
                    }
                });

                publishingTasks.Add(publishingTask);
            }

            Task.Run(() =>
            {
                var spinWait = new SpinWait();
                while (receivedMessages.Count < taskCount * messageCountPerTask)
                {
                    var pollSucceededAtLeastOnce = true;
                    
                    foreach (var memoryChannel in memoryChannels)
                    {
                        pollSucceededAtLeastOnce &= memoryChannel.TryPoll();
                    }

                    if (!pollSucceededAtLeastOnce)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    
                    spinWait.Reset();
                }
            });

            // Act
            publishingTasks.ForEach(x => x.Start());
            Task.WaitAll(publishingTasks.ToArray());

            // Assert
            countdownSignal.Wait(TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(receivedMessages.Count, taskCount * messageCountPerTask);

            for (var i = 0; i < taskCount; i++)
            {
                var taskMessages = receivedMessages.Where(x => x.StartsWith($"T{i}")).ToArray();
                Assert.AreEqual(taskMessages.Length, messageCountPerTask);

                for (var j = 0; j < messageCountPerTask; j++)
                {
                    Assert.AreEqual(taskMessages[j], $"T{i};M{j}");
                }
            }

            memoryChannels.ForEach(x => x.Stop());
        }
    }
}
