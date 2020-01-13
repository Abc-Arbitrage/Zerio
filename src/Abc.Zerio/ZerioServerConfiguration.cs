using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public class ZerioServerConfiguration : ZerioConfiguration
    {
        public int SessionCount { get; set; }

        public ZerioServerConfiguration()
        {
            SessionCount = 16;
        }

        internal override InternalZerioConfiguration ToInternalConfiguration()
        {
            var configuration = base.ToInternalConfiguration();
            
            configuration.SessionCount = SessionCount;
            
            configuration.MaxReceiveCompletionResults *= SessionCount;
            configuration.RequestQueueMaxOutstandingReceives *= SessionCount;
            configuration.ReceivingCompletionQueueSize *= SessionCount;

            return configuration;
        }
    }
}