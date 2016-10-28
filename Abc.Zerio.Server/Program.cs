using System;
using System.Diagnostics;
using System.Text;
using Abc.Zerio.Dispatch;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;
using Abc.Zerio.Server.Serializers;

namespace Abc.Zerio.Server
{
    internal static class Program
    {
        private const int _port = 15698;
        private static int _receivedMessageCount;
        private static readonly Stopwatch _sw = new Stopwatch();

        private static readonly SimpleMessagePool<PlaceOrderMessage> _messagePool = new SimpleMessagePool<PlaceOrderMessage>(65536);

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
                server.ClientConnected += OnClientConnected;
                server.ClientDisconnected += OnClientDisconnected;

                using (server.Subscribe<PlaceOrderMessage>(OnMessageReceived))
                {
                    server.Start();

                    Console.WriteLine("Press enter to quit.");
                    Console.ReadLine();
                }
            }
        }

        private static void OnClientDisconnected(int clientId)
        {
            Console.WriteLine($"Client {clientId} disconnected.");
        }

        private static void OnClientConnected(int clientId)
        {
            Console.WriteLine($"Client {clientId} connected.");
        }

        private static void OnMessageReceived(int clientId, PlaceOrderMessage placeOrderMessage)
        {
            if (_receivedMessageCount == 0)
                _sw.Start();

            var stepcount = 100 * 1000;
            if (++_receivedMessageCount % stepcount == 0)
            {
                Console.WriteLine($"{_receivedMessageCount:N0} in {_sw.Elapsed} ({stepcount / _sw.Elapsed.TotalSeconds:N0}m/s)");
                _sw.Restart();
            }
        }

        private static RioServer CreateServer(IServerConfiguration configuration)
        {
            var serializationEngine = CreateSerializationEngine();
            return new RioServer(configuration, serializationEngine);
        }

        private static SerializationEngine CreateSerializationEngine()
        {
            var serializationRegistry = new SerializationRegistry(Encoding.ASCII);
            serializationRegistry.Register<PlaceOrderMessage, PlaceOrderMessageSerializer>(_messagePool, _messagePool);
            serializationRegistry.Register<OrderAckMessage, OrderAckMessageSerializer>();
            var serializationEngine = new SerializationEngine(serializationRegistry);
            return serializationEngine;
        }
    }
}
