using System;
using System.Diagnostics;
using System.Text;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;
using Abc.Zerio.Server.Serializers;
using Abc.Zerio.Dispatch;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private const int _port = 15698;
        private static int _receivedMessageCount;

        private static readonly SimpleMessagePool<PlaceOrderMessage> _allocator = new SimpleMessagePool<PlaceOrderMessage>(65536);

        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("SERVER...");

            RunRioServer();
        }

        private static void RunRioServer()
        {
            var configuration = new ServerConfiguration(_port);

            using (var server = CreateServer(configuration))
            {
                server.ClientConnected += clientId => OnClientConnected(server, clientId);
                server.ClientDisconnected += clientId => OnClientDisconnected(server, clientId);
                server.Subscribe<PlaceOrderMessage>(OnMessageReceivedOnServer);
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

        private static void OnMessageReceivedOnServer(int clientId, PlaceOrderMessage placeOrderMessage)
        {
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
            var serializationEngine = CreateSerializationEngine();
            return new RioServer(configuration, serializationEngine);
        }

        private static SerializationEngine CreateSerializationEngine()
        {
            var serializationRegistry = new SerializationRegistry(Encoding.ASCII);
            serializationRegistry.Register<PlaceOrderMessage, PlaceOrderMessageSerializer>(_allocator);
            serializationRegistry.Register<OrderAckMessage, OrderAckMessageSerializer>();
            var serializationEngine = new SerializationEngine(serializationRegistry);
            return serializationEngine;
        }
    }
}
