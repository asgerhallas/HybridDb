using System.ComponentModel;

namespace HybridDb.Studio.Infrastructure.Views
{
    public interface IView
    {
        object DataContext { get; set; }
    }

    public interface IWindow : IView
    {
        void Show();
        event CancelEventHandler Closing;
    }
}