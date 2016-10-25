using System.Threading.Tasks;
using Abc.Zerio.Framing;
using Abc.Zerio.Serialization;
using NUnit.Framework;

namespace Abc.Zerio.Tests.ClientServer
{
    public class PingPongTests : ClientServerFixture
    {
        [Test]
        public void should_receive_pong_message()
        {
            Server.MessageReceived += (clientId, messageTypeId, message) => Server.Send(clientId, new Pong { PingId = ((Ping)message).Id });
            Server.Start();

            var taskCompletionSource = new TaskCompletionSource<Pong>();
            Client.MessageReceived += (_, m) => taskCompletionSource.SetResult((Pong)m);

            Client.Connect(ServerEndPoint);
            Client.Send(new Ping { Id = 9876 });

            var received = taskCompletionSource.Task.Wait(500);
            Assert.That(received, Is.True);
            Assert.That(taskCompletionSource.Task.Result.PingId, Is.EqualTo(9876));
        }

        [Test]
        public void should_receive_many_pong_messages()
        {
            Server.MessageReceived += (clientId, messageTypeId, message) => Server.Send(clientId, new Pong { PingId = ((Ping)message).Id });
            Server.Start();

            const int expectedMessageCount = 10 * 1000;

            var lastMessageReceived = new TaskCompletionSource<object>();
            var outOfOrderMessageReceived = new TaskCompletionSource<object>();
            var expectedPingId = 1;
            Client.MessageReceived += (_, m) =>
            {
                var pong = (Pong)m;
                if (pong.PingId != expectedPingId)
                {
                    outOfOrderMessageReceived.TrySetResult(null);
                    return;
                }
                if (pong.PingId == expectedMessageCount)
                {
                    lastMessageReceived.TrySetResult(null);
                    return;
                }
                expectedPingId++;
            };

            Client.Connect(ServerEndPoint);

            for (var pingId = 1; pingId <= expectedMessageCount; pingId++)
            {
                Client.Send(new Ping { Id = pingId });
            }

            var finished = Task.WhenAny(lastMessageReceived.Task, outOfOrderMessageReceived.Task);

            Assert.That(finished.Wait(5000), Is.True, "Timeout");
            Assert.That(outOfOrderMessageReceived.Task.IsCompleted, Is.False, "Out of order message received");
            Assert.That(lastMessageReceived.Task.IsCompleted, Is.True, "Last message was not received");
        }

        protected override void ConfigureSerialization(SerializationRegistries registries)
        {
            registries.ForBoth(r => r.Register<Ping, PingSerializer>());
            registries.ForBoth(r => r.Register<Pong, PongSerializer>());
        }

        public class Ping
        {
            public long Id { get; set; }
        }

        public class Pong
        {
            public long PingId { get; set; }
        }

        public class PingSerializer : BinaryMessageSerializer<Ping>
        {
            public override void Serialize(Ping message, UnsafeBinaryWriter binaryWriter)
            {
                binaryWriter.Write(message.Id);
            }

            public override void Deserialize(Ping message, UnsafeBinaryReader binaryReader)
            {
                message.Id = binaryReader.ReadInt32();
            }
        }

        public class PongSerializer : BinaryMessageSerializer<Pong>
        {
            public override void Serialize(Pong message, UnsafeBinaryWriter binaryWriter)
            {
                binaryWriter.Write(message.PingId);
            }

            public override void Deserialize(Pong message, UnsafeBinaryReader binaryReader)
            {
                message.PingId = binaryReader.ReadInt32();
            }
        }
    }
}
