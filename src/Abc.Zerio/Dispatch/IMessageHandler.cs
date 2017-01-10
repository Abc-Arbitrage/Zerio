namespace Abc.Zerio.Dispatch
{
    public interface IMessageHandler
    {
        void Handle(int clientId, object message);
    }
}
