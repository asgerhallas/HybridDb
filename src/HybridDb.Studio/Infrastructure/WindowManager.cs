using System;
using HybridDb.Studio.Infrastructure.ViewModels;
using HybridDb.Studio.Infrastructure.Views;

namespace HybridDb.Studio.Infrastructure
{
    public class WindowManager
    {
        private readonly IViewModelFactory viewModelFactory;

        public WindowManager(IViewModelFactory viewModelFactory)
        {
            this.viewModelFactory = viewModelFactory;
        }

        public void OpenWindow<T>() where T : ViewModel
        {
            var viewModel = viewModelFactory.Create<T>();
            var view = ViewLocator.LocateView(viewModel);

            var window = view as IWindow;
            if (window == null)
            {
                throw new InvalidOperationException(String.Format("View '{0}' located for '{1}' does not implement IWindow", viewModel.GetType().Name, view.GetType().Name));
            }

            window.DataContext = viewModel;
            window.Show();
        }
    }
}