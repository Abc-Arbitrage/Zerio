using System;
using System.Net;
using System.Text;
using Abc.Zerio.Serialization;
using NUnit.Framework;

namespace Abc.Zerio.Tests.ClientServer
{
    [TestFixture]
    public abstract class ClientServerFixture
    {
        [SetUp]
        public virtual void Setup()
        {
            ClientConfiguration = new ClientConfiguration();
            ServerConfiguration = new ServerConfiguration(0);

            var serializationRegistries = new SerializationRegistries();
            ConfigureSerialization(serializationRegistries);

            Client = new RioClient(ClientConfiguration, new SerializationEngine(serializationRegistries.Client));
            Server = new RioServer(ServerConfiguration, new SerializationEngine(serializationRegistries.Server));
        }

        protected virtual void ConfigureSerialization(SerializationRegistries registries)
        {
        }

        [TearDown]
        public void Teardown()
        {
            Client.Dispose();
            Server.Dispose();
        }

        protected ServerConfiguration ServerConfiguration { get; set; }
        protected ClientConfiguration ClientConfiguration { get; set; }
        protected RioClient Client { get; set; }
        protected RioServer Server { get; set; }
        protected IPEndPoint ServerEndPoint => new IPEndPoint(IPAddress.Loopback, Server.ListeningPort);

        protected class SerializationRegistries
        {
            public SerializationRegistry Client { get; } = new SerializationRegistry(Encoding.ASCII);
            public SerializationRegistry Server { get; } = new SerializationRegistry(Encoding.ASCII);

            public void ForBoth(Action<SerializationRegistry> action)
            {
                action.Invoke(Client);
                action.Invoke(Server);
            }
        }
    }
}
