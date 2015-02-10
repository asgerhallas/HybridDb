using System;
using HybridDb.Studio.Views;

namespace HybridDb.Studio.ViewModels
{
    public class MainViewModel : ViewModel
    {
        private readonly IViewLocator viewLocator;
        private IView contentView;

        public MainViewModel(IViewLocator viewLocator)
        {
            this.viewLocator = viewLocator;
            StatusText = "Does it work?";
            Open<DocumentView>();
        }

        public string StatusText { get; set; }

        public IView ContentView
        {
            get { return contentView; }
            set
            {
                if (Equals(value, contentView)) return;
                contentView = value;
                OnPropertyChanged();
            }
        }

        public void Open<TView>() where TView : IView
        {
            ContentView = viewLocator.Create<TView>();
        }
    }

    public interface IViewLocator
    {
        T Create<T>() where T : IView;
    }
}