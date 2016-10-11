using Abc.Zerio.Core;

namespace Abc.Zerio
{
    public interface IServerConfiguration : ISessionConfiguration, IWorkerConfiguration
    {
        int SessionCount { get; }
        int WorkerCount { get; }
        int ListeningPort { get; }
    }
}
