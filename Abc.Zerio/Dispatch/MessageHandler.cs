namespace Abc.Zerio.Dispatch
{
    public abstract class MessageHandler<T> : IMessageHandler
    {
        public void Handle(int clientId, object message)
        {
            Handle(clientId, (T)message);
        }

        protected abstract void Handle(int clientId, T message);
    }
}
