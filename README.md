# Zerio

## Overview

Zerio is a small experimental project which aims to provide **a very basic TCP client/server messaging API based on Windows Registered I/O (RIO)**. The implementation is in **C# (.NET)** and is performance-oriented. The initial design of the project allows a bidirectionnal messaging communication between a server and several clients without generating any garbage (**zero allocation programming**) and **without unecessary data copies** (framing, serialization and deserialization of messages are directly based on the underlying RIO buffers).

## Disclaimer

**Zerio is very much a work in progress and more a proof of concept than anything else at this stage. It is not production ready yet.**

## Messages and message types ids

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

Each message type has an associated `MessageTypeId` in Zerio. Right now, the message type id is a struct that wraps a simple unsigned integer value and which is supposed to uniquely identify a message type. Zerio is able to generate message type ids for you but it is possible to register specific ids using static methods on the `MessageTypeId` struct:

```csharp
var messageTypeId = new MessageTypeId(42);
MessageTypeId.Register(typeof(PlaceOrderMessage), messageId);
```

 If no specific id is registered for a type, it will be computed using a pretty naive method by default (a CRC32 hash of the message full type name by convention – changing a message type name would then be a breaking change).

## Serializers
You need to write a binary serializer for your message, implementing the `IBinaryMessageSerializer` interface. An generic abstract base class, `BinaryMessageSerializer<T>` is also provided and is generally what you want to inherit from most of the time. The API is voluntarily low-level. Note that the `Deserialize` method already provides an instance of the message you need to initialize. You may want to handle versionning here, as well as keeping your implementation allocation free.

```csharp
public class PlaceOrderMessageSerializer : BinaryMessageSerializer<PlaceOrderMessage>
{
    public void Serialize(PlaceOrderMessage message, UnsafeBinaryWriter binaryWriter)
    {
        binaryWriter.Write(message.InstrumentId);
        binaryWriter.Write(message.Price);
        binaryWriter.Write(message.Quantity);
        binaryWriter.Write((byte)message.Side);
    }

    public void Deserialize(PlaceOrderMessage message, UnsafeBinaryReader binaryReader)
    {
        message.InstrumentId = binaryReader.ReadInt32();
        message.Price = binaryReader.ReadDouble();
        message.Quantity = binaryReader.ReadDouble();
        message.Side = (OrderSide)binaryReader.ReadByte();
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
private static void OnMessageReceived(MessageTypeId messageTypeId, object message)
{
    var placeOrderMessage = message as PlaceOrderMessage;
    if (placeOrderMessage != null)
    {
        // do something with the message
    }
}
```

Again, the API is minimalist, and non-generic. You'll have to handle the message type dispatch yourself if needed. The `messageTypeId` parameter uniquely identifies the message type.

## Server

### Creation

Similarily to the client, to create a server you just have to do the following: 

```csharp
using (var server = new RioServer(configuration, serializationEngine))
{
    server.ClientConnected += OnClientConnected;
    server.ClientDisconnected += OnClientDisconnected;
    server.MessageReceived += OnMessageReceived;
    server.Start();
}
```

### Sending a message

To send a message, you'll need the id of the client you want to send the message to. This id can be retrieved during the client connection:

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
private static void OnMessageReceived(int clientId, MessageTypeId messageTypeId, object message)
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
serializationRegistry.Register<PlaceOrderMessage, PlaceOrderMessageSerializer>();
var serializationEngine = new SerializationEngine(serializationRegistry);
```

The registry is where you can register messages and their corresponding serializers.

## Allocators and releasers

Because you may want to receive messages **without generating garbage**, Zerio provides you a way to register allocators and releasers that will be used by the library when handling incoming messages. You can optionally provide them when registering a message type in the `SerializationRegistry`. By default, Zerio will use a simple `HeapAllocator` and no releaser. You can also find in the project a `SimpleMessagePool`, which is both an allocator and a releaser:

```csharp
var messagePool = new SimpleMessagePool<PlaceOrderMessage>(256);
var registry = new SerializationRegistry(Encoding.ASCII);
registry.Register<PlaceOrderMessage, PlaceOrderMessageSerializer>(messagePool, messagePool);
```

If you provide an allocator, Zerio will use it upon message reception, to get the instance that will be used for the deserialization. If you provide a releaser, Zerio will use it right after the message handling, that is, the `OnMessageReceived` event invocation. If you want to control when the received message need to be released, you can provide an allocator and no releaser; you'll then be in charge of releasing the received message.

# Internals

## Protocol

The protocol is pretty basic. Each frame contening one message is length prefixed. The frame content contains the message type id underlying value, and the actual serialized message. The serialization is done by the provided custom binary serializer.

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


## Sessions and workers

Windows Registered I/O (RIO) uses a programming model where all I/O operations are asynchronous. You enqueue sends and receives operations of one socket, and you can observe one or several completion queues to be notified when these operations are completed. In Zerio, we have the concept of **sessions** and **workers**.

* A `RioSession` wraps a socket that is used for the bidirectional communication between a client and a server. A client has one active session, whereas a server has one session per client.

* A `RioWorker` is a component responsible for handling asynchronous I/O operation completions. Since it's possible to share a RIO completion queue among several sockets, it's also possible to have a `RioWorker` instance responsible for several sessions I/O operation completions. A client only needs one `RioWorker` because it only has one session, but a Zerio server can be configured to use several workers. This allows to scale the handling of incoming messages. Typically, a worker wraps a RIO completion queue and has a background thread responsible for polling it and process completion notifications.

## Buffer management and serialization

Windows Registered I/O (RIO) requires you to register and manage the buffers that will be used for sends and receives on the sockets. You typically register a large buffer and then refer to segments of it for your I/O operations. In Zerio, each `RioSession` has its own RIO registered buffer and slices it in fixed size segments. When a send occurs on the session, Zerio use as much segment buffers as needed. A simple message is usually way smaller than a buffer segment so only one is needed. However, the serialization engine is using a specific `UnsafeBinaryWriter` which can acquire several buffers if the serialized message data should span over multiple segments (several send operations would then logically be enqueued). The serialization of the message occurs directly to the underlying RIO buffers and **no unecessary copies are needed**.

## Framing and deserialization

When a receive operation completes on the session socket, the received bytes are offered to the `MessageFramer`. The message framer is able to frame messages and to reference the underlying RIO buffers contening them. Typically, a receive operation and its associated RIO buffer segment concerns one or many messages. But if a message spans across several buffer segments, the `MessageFramer` is able to keep track of all of them, and a specific `UnsafeBinaryReader` is used by the serialization engine to materialize the message from the multiple buffer segments, **avoiding any extra copy**. The buffer segments are automatically released as soon as possible by the message framer.

## ~~Zerio~~ Zero allocation

Both `RioClient` and `RioServer` can be used to communicate with each other without allocating any .NET object instances on the heap, thus avoiding any garbage collection to be triggered. However, the connection phase between a client and a server is not garbage free. To prevent any allocation from happening, you have to implement your binary serializer the correct way, as well as to handle message pooling and releasing properly.

## Queue sizing

In Windows Registered I/O (RIO), there are two types of queues:
 - **Request queues**: associated to a socket, the request queue is how you initiate asynchronous operations by enqueuing send and receive requests. Additionally, you can configure the maximum outstanding send operation count and the maximum outstanding receive operation count.
 - **Completion queues**: each request queue needs be associated to a completion queue for the send operation completions, and to a completion queue for the receive opereration completions. The same completion queue can be used for both, and can even be shared among several request queues.
 
In Zerio, the queue sizing policy is very naive for now. Each session has a socket, a request queue, and two buffer managers (one for sends and one for receives). Since the buffer managers are bounded and block on acquiring when no buffer is available, we use the send buffer count as the max outstanding send count, and the receive buffer count as the max outstanding receive count. Then, we just compute the completion queue size based on the total max outstanding request counts, and never resize it. If a completion queue is shared among several request queues, we take the session count into consideration.

