using System.Collections.ObjectModel;
using HybridDb.Studio.Infrastructure.ViewModels;

namespace HybridDb.Studio.ViewModels
{
    public class ListViewModel : ViewModel
    {
        public ListViewModel()
        {
            Rows = new ObservableCollection<object>
            {
                new { Test1 = "Hej", Test2 = "Med", Test3 = "Dig" },
                new { Test1 = "Farvel", Test2 = "til", Test3 = "mig" },
            };
        }

        public string Title
        {
            get { return "List"; }
        }

        public ObservableCollection<object> Rows { get; private set; }
    }
}