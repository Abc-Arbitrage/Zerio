namespace Abc.Zerio.Serialization
{
    public interface IMessageReleaser<in T> : IMessageReleaser
    {
        void Release(T item);
    }
}
