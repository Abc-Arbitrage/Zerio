using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Abc.Zerio.Dispatch
{
    public class MessageDispatcher
    {
        private readonly object _syncRoot = new object();

        private Dictionary<Type, List<IMessageHandler>> _handlersByType = new Dictionary<Type, List<IMessageHandler>>();

        public IDisposable AddHandler<T>(IMessageHandler handler)
        {
            lock (_syncRoot)
            {
                var messageType = typeof(T);

                List<IMessageHandler> originalHandlerList;
                if (!_handlersByType.TryGetValue(messageType, out originalHandlerList))
                    originalHandlerList = new List<IMessageHandler>();

                var handlerListCopy = originalHandlerList.Where(x => x != handler).ToList();
                handlerListCopy.Add(handler);

                _handlersByType = CopyHandlersByType(messageType, handlerListCopy);

                return new Subscription<T>(this, handler);
            }
        }

        private void RemoveHandler<T>(IMessageHandler handler)
        {
            lock (_syncRoot)
            {
                var messageType = typeof(T);

                List<IMessageHandler> originalHandlerList;
                if (!_handlersByType.TryGetValue(messageType, out originalHandlerList))
                    return;

                var handlerListCopy = originalHandlerList.Where(x => x != handler).ToList();

                _handlersByType = CopyHandlersByType(messageType, handlerListCopy);
            }
        }

        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField", Justification = "No lock for reads, copied on write.")]
        public void Dispatch(int clientId, object message)
        {
            List<IMessageHandler> handlers;
            if (!_handlersByType.TryGetValue(message.GetType(), out handlers))
                return;

            // todo: more granular exception catching
            foreach (var handler in handlers)
            {
                handler.Handle(clientId, message);
            }
        }

        private Dictionary<Type, List<IMessageHandler>> CopyHandlersByType(Type messageType, List<IMessageHandler> newHandlerList)
        {
            var newHandlersByType = new Dictionary<Type, List<IMessageHandler>>();

            foreach (var handlerList in _handlersByType)
            {
                if (handlerList.Key != messageType)
                    newHandlersByType.Add(handlerList.Key, handlerList.Value);
            }

            if (newHandlerList.Count != 0)
                newHandlersByType.Add(messageType, newHandlerList);

            return newHandlersByType;
        }

        private struct Subscription<T> : IDisposable
        {
            private readonly MessageDispatcher _dispatcher;
            private readonly IMessageHandler _handler;

            public Subscription(MessageDispatcher dispatcher, IMessageHandler handler)
            {
                _dispatcher = dispatcher;
                _handler = handler;
            }

            public void Dispose()
            {
                _dispatcher.RemoveHandler<T>(_handler);
            }
        }
    }
}
