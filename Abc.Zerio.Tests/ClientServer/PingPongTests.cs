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
            Server.MessageReceived += (clientId, m) => Server.Send(clientId, new Pong { PingId = ((Ping)m).Id });
            Server.Start();

            var taskCompletionSource = new TaskCompletionSource<Pong>();
            Client.MessageReceived += m => taskCompletionSource.SetResult((Pong)m);

            Client.Connect(ServerEndPoint);
            Client.Send(new Ping { Id = 9876 });

            var received = taskCompletionSource.Task.Wait(200);
            Assert.That(received, Is.True);
            Assert.That(taskCompletionSource.Task.Result.PingId, Is.EqualTo(9876));
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
