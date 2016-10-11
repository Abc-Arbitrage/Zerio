# Zerio

## Overview

Zerio is a small experimental project which aims to provide **a very basic TCP client/server messaging API based on Windows Registered I/O (RIO)**. The implementation is in **C# (.NET)** and is performance-oriented. The initial design of the project allows a bidirectionnal messaging communication between a server and several clients without generating any garbage (**zero allocation programming**) and **without unecessary data copies** (framing, serialization and deserialization of messages are directly based on the underlying RIO buffers).

## Disclaimer

Zerio is more a proof of concept than anything else at this stage, and is not production ready yet.

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

### Creation
This is how you create a client and to connect to a server. We'll see what the required `SerializationEngine` is in a minute. Note that the API offers different C# events you can register to, in order to receive messages from the server for example.

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

### Sending a message

Just instanciate a message (or pool, or reuse an existing instance), and send it:

```csharp
    var message = new PlaceOrderMessage();
    client.Send(message);
```

### Receiving a message

By subscribing to the `OnMessageReceived` event, you can be notified on message reception:

```csharp
    private static void OnMessageReceived(object message)
    {
        var placeOrderMessage = message as PlaceOrderMessage;
        if (placeOrderMessage != null)
        {
            // do something with the message
        }
    }
```

Again, the API is minimalist, and non-generic. You'll have to handle the message type dispatch yourself if needed.

## Server

### Creation

Similarily to the client, to create a server you just have to do the following: 

```csharp
    using (var server = new RioServer(configuration, new SessionManager(configuration), serializationEngine))
    {
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.MessageReceived += OnMessageReceived;
            server.Start();

    }
```

### Sending a message

To send a message, you'll need the id of the client you want to send the message to. This id can be retreived on the client connection:

```csharp
    private static void OnClientDisconnected(int clientId)
    {
    }
 ```
 
Then you simply have to instanciate a message (or pool, or reuse an existing instance), and send it:

```csharp
    var message = new PlaceOrderMessage();
    server.Send(clientId, message);
```

### Receiving a message

By subscribing to the `OnMessageReceived` event, you can be notified on message reception. The client from which the message is received is provided as an event argument:

```csharp
    private static void OnMessageReceived(int clientId, object message)
    {
        var placeOrderMessage = message as PlaceOrderMessage;
        if (placeOrderMessage != null)
        {
            // do something with the message
        }
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

The registry is where you can register messages and their corresponding serializer.

## Allocators and releasers

Because you may want to receive messages without generating garbage, Zerio provides you a way to register allocators and releasers for your messages, that will be used by the library at when handling incoming messages. You can optionally provide them when registering a message type in the `SerializationRegistry`. By default, Zerio will use a simple `HeapAllocator` and no releaser. You can also find in the project a `SimpleMessagePool`, which is both an allocator and a releaser:

```csharp
    var messagePool = new SimpleMessagePool<PlaceOrderMessage>(256);
    var registry = new SerializationRegistry(Encoding.ASCII);
    registry.AddMapping<PlaceOrderMessage, PlaceOrderMessageSerializer>(messagePool, messagePool);
```

If you provide an allocator, Zerio will use it upon message reception, to get a the instance that will be used for the deserialization. If you provide a releaser, Zerio will use it right after the message handling, that is, the `OnMessageReceived` event invocation. If you want to control when the received message need to be released, you can provide an allocator and no releaser; you'll then be in charge of releasing the received message.

# Internals

## Protocol

The protocol is pretty basic. Each frame contening one message is length prefixed. The frame content contains the message type id, and the actual serialized message. The serialization is done by the provided custom binary serializer.

Frame

| Data          | Type          | Length (bytes) |
| ------------- |---------------|----------------|
| Frame length  | int           | 4              |
| Frame body    | binary      |   Frame length |

Frame body

| Data                    | Type          | Length (bytes)   |
| ------------------------|---------------|------------------|
| Message type id         | uint          | 4                |
| Serialized message      | binary        | Frame length - 4 |

Right now, the message type id is an unsigned integer value that is supposed to uniquely identify a message type. The current implementation is pretty naive and computes a CRC32 hash of the message full type name by convention. Changing a message type name would be a breaking change.

