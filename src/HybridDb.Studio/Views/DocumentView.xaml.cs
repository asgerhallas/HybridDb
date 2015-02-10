using System.Windows.Controls;
using HybridDb.Studio.ViewModels;

namespace HybridDb.Studio.Views
{
    /// <summary>
    /// Interaction logic for DocumentView.xaml
    /// </summary>
    public partial class DocumentView : UserControl, IView
    {
        public DocumentView(DocumentViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
