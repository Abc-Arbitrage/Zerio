namespace Abc.Zerio.Core
{
    public interface ICompletionHandler
    {
        void OnRequestCompletion(int sessionId, RioRequestContextKey requestContextKey, int bytesTransferred);
    }
}
