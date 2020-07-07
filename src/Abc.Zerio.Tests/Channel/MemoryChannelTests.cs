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
            const int messageCountPerTask = 10_000;
            var countdownSignal = new CountdownEvent(taskCount * messageCountPerTask);
            
            var receivedMessages = new List<string>();
            
            var memoryChannel = new MemoryChannel();
            memoryChannel.MessageReceived += (messageBytes )=>
            {
                receivedMessages.Add(Encoding.ASCII.GetString(messageBytes.ToArray()));
                countdownSignal.Signal();
            };            
            
            memoryChannel.Start();
            
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
    }
}
