using Caliburn.Micro;
using HybridDb.Studio.Models;

namespace HybridDb.Studio.ViewModels
{
    public class DocumentViewModel : Screen
    {
        private Document document;

        public DocumentViewModel(Document document)
        {
            Document = document;
        }

        public Document Document
        {
            get { return document; }
            set
            {
                if (Equals(value, document)) return;
                document = value;
                NotifyOfPropertyChange(() => Document);
            }
        }
    }
}