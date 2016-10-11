namespace Abc.Zerio.Server.Messages
{
    public class PlaceOrderMessage
    {
        public long Id;
        public int InstrumentId;
        public double Price;
        public double Quantity;
        public OrderSide Side;

        public override string ToString()
        {
            return $"Id: {Id}, InstrumentId: {InstrumentId}, Price: {Price}, Quantity: {Quantity}, Side: {Side}";
        }
    }
}
