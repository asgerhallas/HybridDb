using System;
using System.Windows.Controls;

namespace HybridDb.Studio.Infrastructure.Views
{
    public abstract class View : UserControl, IView
    {
        public void Dispose()
        {
            var disposable = DataContext as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}