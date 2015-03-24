namespace HybridDb.Studio.Infrastructure.ViewModels
{
    public interface IViewModelFactory
    {
        T Create<T>() where T : ViewModel;
    }
}