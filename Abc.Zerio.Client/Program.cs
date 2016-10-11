using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Abc.Zerio.Serialization;
using Abc.Zerio.Server.Messages;
using Abc.Zerio.Server.Serializers;

namespace Abc.Zerio.Client
{
    internal static class Program
    {
        private const int _port = 15698;
        private const int _messageCount = 10 * 1000 * 1000;

        private static int _receivedMessageCount;
        private static readonly Stopwatch _sw = new Stopwatch();
        private static readonly SimpleMessagePool<OrderAckMessage> _orderAckMessagePool = new SimpleMessagePool<OrderAckMessage>(65536);

        private static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            Console.WriteLine("CLIENT...");

            RunRioClient();

            Console.WriteLine("Press enter to quit.");
            Console.ReadLine();
        }

        private static void RunRioClient()
        {
            var configuration = new ClientConfiguration();
            using (var client = CreateClient(configuration))
            {
                client.Connected += OnClientConnected;
                client.Disconnected += OnClientDisconnected;
                client.MessageReceived += OnMessageReceived;

                var address = Dns.GetHostAddresses(Environment.MachineName).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                var endPoint = new IPEndPoint(address, _port);
                client.Connect(endPoint);

                var instance = new PlaceOrderMessage();

                var sw = Stopwatch.StartNew();
                for (var i = 0; i < _messageCount; i++)
                {
                    instance.Id = i;
                    instance.InstrumentId = i;
                    instance.Price = i;
                    instance.Quantity = i;
                    instance.Side = (OrderSide)(i % 2);
                    client.Send(instance);
                }
                Console.WriteLine($"{_messageCount:N0} in {sw.Elapsed} ({_messageCount / sw.Elapsed.TotalSeconds:N0}m/s)");
            }
        }

        private static void OnClientDisconnected()
        {
            Console.WriteLine("Disconnected");
        }

        private static void OnClientConnected()
        {
            Console.WriteLine("Connected");
        }

        private static void OnMessageReceived(object message)
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

        private static RioClient CreateClient(ClientConfiguration configuration)
        {
            var serializationEngine = CreateSerializationEngine();
            return new RioClient(configuration, serializationEngine);
        }

        private static SerializationEngine CreateSerializationEngine()
        {
            var serializationRegistry = new SerializationRegistry(Encoding.ASCII);
            serializationRegistry.AddMapping<PlaceOrderMessage, PlaceOrderMessageSerializer>();
            serializationRegistry.AddMapping<OrderAckMessage, OrderAckMessageSerializer>(_orderAckMessagePool, _orderAckMessagePool);
            var serializationEngine = new SerializationEngine(serializationRegistry);
            return serializationEngine;
        }
    }
}
