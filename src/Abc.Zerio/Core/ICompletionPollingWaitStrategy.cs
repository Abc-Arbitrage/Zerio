namespace Abc.Zerio.Core
{
    internal interface ICompletionPollingWaitStrategy
    {
        void Wait();
        void Reset();
    }
}
