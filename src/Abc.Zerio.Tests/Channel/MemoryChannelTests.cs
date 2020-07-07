using System;
using System.Text;
using System.Threading;
using Abc.Zerio.Channel;
using NUnit.Framework;
namespace Abc.Zerio.Tests.Channel
{
    [TestFixture]
    public class MemoryChannelTests
    {
        [Test, Explicit]
        public void ManualTest()
        {
            var memoryChannel = new MemoryChannel();
            memoryChannel.MessageReceived += OnMessageReceived;
            memoryChannel.Start();

            for (var i = 0; i < 10; i++)
            {
                var messageBytes = Encoding.ASCII.GetBytes($"message {i}");
                memoryChannel.Send(messageBytes.AsSpan());
                
                Thread.Sleep(1000);
            }
            
            memoryChannel.Stop();
        }

        private void OnMessageReceived(ReadOnlySpan<byte> messageBytes)
        {
            Console.WriteLine(Encoding.ASCII.GetString(messageBytes.ToArray()));
        }
    }
}
