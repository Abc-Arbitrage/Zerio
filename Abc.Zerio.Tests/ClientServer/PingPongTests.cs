using System.IO;
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
            registries.ForBoth(r => r.AddMapping<Ping, PingSerializer>());
            registries.ForBoth(r => r.AddMapping<Pong, PongSerializer>());
        }

        public class Ping
        {
            public long Id { get; set; }
        }

        public class Pong
        {
            public long PingId { get; set; }
        }

        public class PingSerializer : IBinaryMessageSerializer
        {
            public void Serialize(object message, UnsafeBinaryWriter binaryWriter)
            {
                binaryWriter.Write(((Ping)message).Id);
            }

            public void Deserialize(object message, UnsafeBinaryReader binaryReader)
            {
                ((Ping)message).Id = binaryReader.ReadInt32();
            }
        }

        public class PongSerializer : IBinaryMessageSerializer
        {
            public void Serialize(object message, UnsafeBinaryWriter binaryWriter)
            {
                binaryWriter.Write(((Pong)message).PingId);
            }

            public void Deserialize(object message, UnsafeBinaryReader binaryReader)
            {
                ((Pong)message).PingId = binaryReader.ReadInt32();
            }
        }
    }
}
