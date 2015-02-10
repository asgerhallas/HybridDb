using System.Windows.Input;

namespace HybridDb.Studio
{
    public interface IRelayCommand : ICommand
    {
        void RaiseCanExecuteChanged();
    }
}