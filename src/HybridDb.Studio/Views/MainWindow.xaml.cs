using System;
using System.Windows;
using HybridDb.Studio.Infrastructure.Views;
using HybridDb.Studio.ViewModels;

namespace HybridDb.Studio.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IView
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;

            Closing += (sender, args) => Dispose();
        }

        public void Dispose()
        {
            ((IDisposable)DataContext).Dispose();
        }
    }
}
