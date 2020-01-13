using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public class ZerioClientConfiguration : ZerioConfiguration
    {
        internal override InternalZerioConfiguration ToInternalConfiguration()
        {
            var configuration = base.ToInternalConfiguration();
            configuration.SessionCount = 1;
            return configuration;
        }
    }
}