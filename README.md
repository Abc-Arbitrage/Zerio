# Zerio

[![Build](https://github.com/Abc-Arbitrage/Zerio/workflows/Build/badge.svg)](https://github.com/Abc-Arbitrage/Zerio/actions?query=workflow%3ABuild)

## Overview

Zerio is a small experimental project which aims to provide **a very basic TCP client/server messaging API based on Windows Registered I/O (RIO)**. The implementation is in **C# (.NET)** and is performance-oriented. The initial design of the project allows a bidirectionnal messaging communication between a server and several clients without generating any garbage (**zero allocation programming**).

## Disclaimer

**Zerio is very much a work in progress and more a proof of concept than anything else at this stage. It is not production ready yet.**

## Server

To create a server you just have to do the following: 

```csharp
using (var server = new ZerioServer(48654))
{
    server.Start("server");
    
    // server is ready to be used
    // ...
    
    server.Stop();
}
```

## Client

This is how you create a client and connect to a server.

```csharp
using (var client = new ZerioClient(new IPEndPoint(IPAddress.Loopback, 48654))
{
    client.Start("client");

    // client is ready to be used
    // ...
}
```

## Sending messages

Using the client to send a message is dead simple: you just have to pass a `ReadOnlySpan<byte>` to the `Send` method:

```csharp
var message = Encoding.ASCII.GetBytes("oui")
client.Send(message);
```

From the server, it's very similar. The only difference is that you will need the id of the client you want to send the message to. This id can be retrieved during the client connection:

```csharp
server.ClientConnected += OnClientConnected;

// ...

private static void OnClientConnected(string peerId)
{
}
 ```
 
Then you simply have to send a message to it:

```csharp
var message = Encoding.ASCII.GetBytes("quoi")
server.Send(peerId, message);
```

# Internals

## ~~Zerio~~ Zero allocation

Both `ZerioClient` and `ZerioServer` can be used to communicate with each other without allocating any .NET object instances on the heap, thus avoiding any garbage collection to be triggered. However, the connection phase between a client and a server is not garbage free.

## Design overview

![Design overview](https://github.com/Abc-Arbitrage/Zerio/blob/master/doc/zerio-overview.png)

## Sending path

![Sending path](https://github.com/Abc-Arbitrage/Zerio/blob/master/doc/zerio-sending-path.png)

## Receiving path

![Receiving path](https://github.com/Abc-Arbitrage/Zerio/blob/master/doc/zerio-receiving-path.png)
