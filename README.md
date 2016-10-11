# Zerio

Zerio is a small experimental project which aims to provide **a very basic TCP client/server messaging API based on Windows Registered I/O (RIO)**. The implementation is in **C# (.NET)** and is performance-oriented. The initial design of the project allows a bidirectionnal messaging communication between a server and several clients without generating any garbage (**zero allocation programming**) and **without unecessary data copies** (framing, serialization and deserialization of messages are directly based on the underlying RIO buffers).

