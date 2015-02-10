namespace HybridDb.Studio.ViewModels
{
    public class DocumentViewModel : ViewModel
    {
        private string document;

        public DocumentViewModel()
        {
            Document = "Massere af Jason!";
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