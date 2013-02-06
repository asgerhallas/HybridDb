using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Linq;
using Energy10.Infrastructure.Persistence;
using HybridDb.Schema;
using HybridDb.Studio.Models;
using Newtonsoft.Json.Linq;

namespace HybridDb.Studio.ViewModels
{
    public class DocumentViewModel : INotifyPropertyChanged
    {
        readonly DocumentStore store;
        Document selectedDocument;
        string documentId;
        ITable selectedTable;
        string statusMessage;
        bool loading;

        public DocumentViewModel()
        {
            store = new DocumentStore("data source=.;Initial Catalog=Energy10;Integrated Security=True");
            DocumentStoreConfigurator.ConfigureStore(store);

            Tables = store.Configuration.Tables.Select(x => x.Value).ToList();
            SelectedTable = Tables.First();
            DocumentId = "3423A819-BCBB-464C-9DB1-0006367895E3";
        }

        public IEnumerable<ITable> Tables { get; private set; }

        public string StatusMessage
        {
            get { return statusMessage; }
            set
            {
                statusMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        public ITable SelectedTable
        {
            get { return selectedTable; }
            set
            {
                selectedTable = value;
                OnPropertyChanged("SelectedTable");
            }
        }

        public bool Loading
        {
            get { return loading; }
            private set
            {
                loading = value;
                OnPropertyChanged("Loading");
                OnPropertyChanged("CanSave");
                OnPropertyChanged("CanFind");
            }
        }

        public bool CanFind
        {
            get { return !Loading; }
        }

        public bool CanSave
        {
            get { return !Loading || SelectedDocument != null; }
        }

        public string DocumentId
        {
            get { return documentId; }
            set
            {
                documentId = value;
                OnPropertyChanged("DocumentId");
            }
        }

        public Document SelectedDocument
        {
            get { return selectedDocument; }
            set
            {
                selectedDocument = value;
                OnPropertyChanged("SelectedDocument");
                OnPropertyChanged("CanSave");
            }
        }

        void OnFind(object sender, EventArgs eventArgs)
        {
            var table = SelectedTable;
            if (table == null)
                return;

            Guid documentId;
            if (!Guid.TryParse(DocumentId, out documentId))
            {
                MessageBox.Show("Can not parse id as Guid", "Error", MessageBoxButton.OK);
                return;
            }

            Loading = true;
            FindDocument(table, documentId);
            Loading = false;
        }

        void FindDocument(ITable table, Guid documentId)
        {
            StatusMessage = string.Format("Getting document {0} from table {1}", documentId, table.Name);
            var sw = Stopwatch.StartNew();
            
            QueryStats stats;
            IDictionary<IColumn, object> projections = store.Get(table, documentId);
            if (projections == null)
            {
                MessageBox.Show(string.Format("Could not find document {0} in {1}", documentId, table.Name),
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            var json = (JObject) store.Configuration.Serializer.Deserialize((byte[]) projections[table.DocumentColumn], typeof (JObject));
            SelectedDocument = new Document(table, json.ToString(), projections);

            StatusMessage = string.Format("Found document {0} in {1}ms", documentId, sw.ElapsedMilliseconds);
        }

        void OnSave(object sender, EventArgs eventArgs)
        {
            if (SelectedDocument == null)
                return;

            Loading = true;
            StatusMessage = string.Format("Saving document {0}", documentId);
            var sw = Stopwatch.StartNew();

            byte[] document = store.Configuration.Serializer.Serialize(JObject.Parse(SelectedDocument.DocumentAsString));
            store.Update(SelectedDocument.Table, SelectedDocument.Id, selectedDocument.Etag ?? Guid.NewGuid(), document, SelectedDocument.Projections.ToDictionary(x => x.Column, x => x.Value));
            StatusMessage = string.Format("Saved document {0} in {1}ms", documentId, sw.ElapsedMilliseconds);

            FindDocument(SelectedDocument.Table, SelectedDocument.Id);
            Loading = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}