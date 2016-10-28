using System;

namespace Abc.Zerio.Dispatch
{
    public static class SubscriptionExtensions
    {
        public static IDisposable Subscribe<T>(this RioClient @this, Action<T> handler)
        {
            return @this.Subscribe<T>(new ClientHandler<T>(handler));
        }

        public static IDisposable Subscribe<T>(this RioClient @this, MessageHandler<T> handler)
        {
            return @this.Subscribe<T>(handler);
        }

        public static IDisposable Subscribe<T>(this RioServer @this, Action<int, T> handler)
        {
            return @this.Subscribe<T>(new ServerHandler<T>(handler));
        }

        public static IDisposable Subscribe<T>(this RioServer @this, MessageHandler<T> handler)
        {
            return @this.Subscribe<T>(handler);
        }

        private class ClientHandler<T> : IMessageHandler
        {
            private readonly Action<T> _handler;

            public ClientHandler(Action<T> handler)
            {
                _handler = handler;
            }

            public void Handle(int clientId, object message)
            {
                _handler.Invoke((T)message);
            }
        }

        private class ServerHandler<T> : IMessageHandler
        {
            private readonly Action<int, T> _handler;

            public ServerHandler(Action<int, T> handler)
            {
                _handler = handler;
            }

            public void Handle(int clientId, object message)
            {
                _handler.Invoke(clientId, (T)message);
            }
        }
    }
}
