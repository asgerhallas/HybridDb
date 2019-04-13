namespace HybridDb.Config
{
    public interface IContainerActivator
    {
        T Resolve<T>();
    }
}