namespace HybridDb.Studio.Infrastructure
{
    public interface IHandle<in T>
    {
        void Handle(T message);
    }
}