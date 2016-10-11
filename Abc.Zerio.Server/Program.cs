using System;
using System.Diagnostics;
using System.Text;
using Abc.Zerio.Core;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;
using Abc.Zerio.Server.Serializers;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private const int _port = 15698;
        private static int _receivedMessageCount;
        private const int _messageCount = 10 * 1000 * 1000;

        private static readonly SimpleMessagePool<PlaceOrderMessage> _allocator = new SimpleMessagePool<PlaceOrderMessage>(65536);

        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            RunRioServer();
        }

        private static void RunRioServer()
        {
            var configuration = ServerConfiguration.Default;
            configuration.ListeningPort = _port;
            using (var server = CreateServer(configuration))
            {
                server.ClientConnected += clientId => OnClientConnected(server, clientId);
                server.ClientDisconnected += clientId => OnClientDisconnected(server, clientId);
                server.MessageReceived += (clientId, message) => OnMessageReceivedOnServer(server, clientId, message);
                server.Start();

                Console.WriteLine("Press enter to quit.");
                Console.ReadLine();
            }
        }

        private static void OnClientDisconnected(RioServer server, int clientId)
        {
            Console.WriteLine($"Client {clientId} disconnected.");
        }

        private static void OnClientConnected(RioServer server, int clientId)
        {
            Console.WriteLine($"Client {clientId} connected.");
        }

        private static readonly Stopwatch _sw = new Stopwatch();

        private static void OnMessageReceivedOnServer(RioServer server, int clientId, object message)
        {
            var placeOrderMessage = (PlaceOrderMessage)message;

            if (_receivedMessageCount == 0)
                _sw.Start();

            var stepcount = 100 * 1000;
            if (++_receivedMessageCount % stepcount == 0)
            {
                Console.WriteLine($"{_receivedMessageCount:N0} in {_sw.Elapsed} ({stepcount / _sw.Elapsed.TotalSeconds:N0}m/s)");
                _sw.Restart();
            }
            _allocator.Release(placeOrderMessage);
        }

        private static RioServer CreateServer(IServerConfiguration configuration)
        {
            var sessionManager = new SessionManager(configuration);
            var serializationEngine = CreateSerializationEngine();
            return new RioServer(configuration, sessionManager, serializationEngine);
        }

        private static SerializationEngine CreateSerializationEngine()
        {
            var serializationRegistry = new SerializationRegistry(Encoding.ASCII);
            serializationRegistry.AddMapping<PlaceOrderMessage, PlaceOrderMessageSerializer>(_allocator);
            serializationRegistry.AddMapping<OrderAckMessage, OrderAckMessageSerializer>();
            var serializationEngine = new SerializationEngine(serializationRegistry);
            return serializationEngine;
        }
    }
}
