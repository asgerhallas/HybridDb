using HybridDb.Studio.Infrastructure.ViewModels;

namespace HybridDb.Studio.ViewModels
{
    public class DocumentViewModel : ViewModel
    {
        private string document;

        public DocumentViewModel()
        {
            Document = "Massere af Jason!";
        }


        public string Title
        {
            get { return "Document"; }
        }


        public string Document
        {
            get { return document; }
            set
            {
                document = value;
                OnPropertyChanged();
            }
        }
    }
}