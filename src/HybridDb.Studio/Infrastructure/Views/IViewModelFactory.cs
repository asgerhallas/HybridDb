using HybridDb.Studio.Infrastructure.ViewModels;

namespace HybridDb.Studio.Infrastructure.Views
{
    public interface IViewModelFactory
    {
        T Create<T>() where T : ViewModel;
    }
}