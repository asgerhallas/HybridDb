using System.Windows.Input;

namespace HybridDb.Studio.Infrastructure.ViewModels
{
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }
}