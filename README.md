# Zerio

## Overview

Zerio is a small experimental project which aims to provide **a very basic TCP client/server messaging API based on Windows Registered I/O (RIO)**. The implementation is in **C# (.NET)** and is performance-oriented. The initial design of the project allows a bidirectionnal messaging communication between a server and several clients without generating any garbage (**zero allocation programming**) and **without unecessary data copies** (framing, serialization and deserialization of messages are directly based on the underlying RIO buffers).

## Messages and serializers

A Zerio message is a simple C# class (POCO):

```csharp
    public class PlaceOrderMessage
    {
        public int InstrumentId;
        public double Price;
        public double Quantity;
        public OrderSide Side;
    }

    public enum OrderSide : byte
    {
        Buy,
        Sell,
    }
```

You need to write a binary serializer for your message, implementing the `IBinaryMessageSerializer` interface. The API is voluntarily low-level. Note that the `Deserialize` method already provide an instance of the message you need to initialize. Note that you may want to handle versionning here, as well as keeping your implementation allocation free.

```csharp
    public class PlaceOrderMessageSerializer : IBinaryMessageSerializer
    {
        public void Serialize(object message, BinaryWriter binaryWriter)
        {
            var placeOrderMessage = (PlaceOrderMessage)message;
            binaryWriter.Write(placeOrderMessage.InstrumentId);
            binaryWriter.Write(placeOrderMessage.Price);
            binaryWriter.Write(placeOrderMessage.Quantity);
            binaryWriter.Write((byte)placeOrderMessage.Side);
        }

        public void Deserialize(object message, BinaryReader binaryReader)
        {
            var placeOrderMessage = (PlaceOrderMessage)message;
            placeOrderMessage.InstrumentId = binaryReader.ReadInt32();
            placeOrderMessage.Price = binaryReader.ReadDouble();
            placeOrderMessage.Quantity = binaryReader.ReadDouble();
            placeOrderMessage.Side = (OrderSide)binaryReader.ReadByte();
        }
    }
```

## Client

This is how you create a client, connect to a server, and use it to send a message. We'll see what the required `SerializationEngine` is in a minute. Note that the API offers different C# events you can register to, in order to receive messages from the server for example.

```csharp
    var serializationEngine = CreateSerializationEngine();
    var configuration = ClientConfiguration.Default;
    using (var client = new RioClient(configuration, serializationEngine))
    {
        client.Connected += OnClientConnected;
        client.Disconnected += OnClientDisconnected;
        client.MessageReceived += OnMessageReceived;

        var endPoint = new IPEndPoint(IPAddress.Loopback, 12345);
        client.Connect(endPoint);

        var message = new PlaceOrderMessage();
        client.Send(message);
    }
```

## Server

Similarily, to create a server you just have to do the following: 

```csharp
    using (var server = new RioServer(configuration, new SessionManager(configuration), serializationEngine))
    {
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.MessageReceived += OnMessageReceived;
            server.Start();

    }
```

## SerializationEngine

When creating a `RioClient`or a `RioServer` you need to pass a properly configured `SerializationEngine` which is a component that knows all existing messages, and how to serialize and deserialize them.

Here is how you create a serialization engine:

```csharp
    var serializationRegistry = new SerializationRegistry(Encoding.ASCII);
    serializationRegistry.AddMapping<PlaceOrderMessage, PlaceOrderMessageSerializer>();
    var serializationEngine = new SerializationEngine(serializationRegistry);
```
