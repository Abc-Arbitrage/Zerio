namespace Abc.Zerio.Core
{
    public interface IWorkerConfiguration
    {
        int MaxCompletionResults { get; }
        int CompletionQueueSize { get; }
    }
}
