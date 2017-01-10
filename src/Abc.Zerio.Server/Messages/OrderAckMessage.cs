namespace Abc.Zerio.Server.Messages
{
    public class OrderAckMessage
    {
        public long Id;

        public override string ToString()
        {
            return $"Id: {Id}";
        }
    }
}
