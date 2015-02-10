using System;
using System.IO;
using HybridDb.Studio.Views;
using Microsoft.Win32;

namespace HybridDb.Studio.ViewModels
{
    public class MainViewModel : ViewModel
    {
        private readonly IViewLocator viewLocator;
        private IView contentView;
        private string statusBarText;

        public MainViewModel(IViewLocator viewLocator)
        {
            this.viewLocator = viewLocator;
            StatusBarText = "Does it work?";
            Open<DocumentView>();

            OpenFile = RelayCommand.Create(ExecuteOpenFile);
        }

        private void ExecuteOpenFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Assemblies (*.dll;*.exe)|*.dll;*.exe|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                StatusBarText = openFileDialog.FileName;
            }
        }

        public IRelayCommand OpenFile { get; set; }

        public string StatusBarText
        {
            get { return statusBarText; }
            set
            {
                if (value == statusBarText) return;
                statusBarText = value;
                OnPropertyChanged();
            }
        }

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