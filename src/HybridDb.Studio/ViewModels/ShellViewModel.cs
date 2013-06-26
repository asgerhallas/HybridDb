using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using HybridDb.Schema;
using HybridDb.Studio.Models;
using Newtonsoft.Json.Linq;

namespace HybridDb.Studio.ViewModels
{
    public class ShellViewModel : Conductor<DocumentViewModel>.Collection.OneActive
    {
        private DocumentStore store;
        private readonly Func<Document, DocumentViewModel> documentViewModelFactory;
        readonly ISettings settings;
        readonly IWindowManager windowManager;

        private string documentId;
        private string tableName;
        private string statusMessage;
        private bool loading;

        public ShellViewModel(Func<Document, DocumentViewModel> documentViewModelFactory, ISettings settings, IWindowManager windowManager)
        {
            this.documentViewModelFactory = documentViewModelFactory;
            this.settings = settings;
            this.windowManager = windowManager;
            store = new DocumentStore(settings.ConnectionString);
        }

        public void OpenSettings()
        {
            windowManager.ShowDialog(settings);
            store.Dispose();
            store = new DocumentStore(settings.ConnectionString);
        }

        public string DocumentId
        {
            get { return documentId; }
            set
            {
                documentId = value;
                NotifyOfPropertyChange(() => DocumentId);
            }
        }

        public string TableName
        {
            get { return tableName; }
            set
            {
                tableName = value;
                NotifyOfPropertyChange(() => TableName);
            }
        }

        public bool CanSaveDocument
        {
            get { return ActiveItem != null; }
        }

        public bool CanFindDocument
        {
            get { return !Loading; }
        }

        public string StatusMessage
        {
            get { return statusMessage; }
            set
            {
                statusMessage = value;
                NotifyOfPropertyChange(() => StatusMessage);
            }
        }

        public bool Loading
        {
            get { return loading; }
            set
            {
                loading = value;
                NotifyOfPropertyChange(() => CanFindDocument);
                NotifyOfPropertyChange(() => Loading);
            }
        }
        
        public void CloseDocument(DocumentViewModel documentViewModel)
        {
            DeactivateItem(documentViewModel, true);
        }

        public void OpenDocument(Document document)
        {
            var documentViewModel = Items.SingleOrDefault(x => x.Document.Id == document.Id && x.Document.Table.Name == document.Table.Name);
            if (documentViewModel == null)
            {
                documentViewModel = documentViewModelFactory(document);
            }
            else
            {
                documentViewModel.Document = document;
            }

            ActivateItem(documentViewModel);
            NotifyOfPropertyChange(() => CanSaveDocument);
        }

        public void SaveDocument(DocumentViewModel documentViewModel)
        {
            if (ActiveItem == null)
                return;

            var document = documentViewModel.Document;

            Loading = true;
            StatusMessage = string.Format("Saving document {0}", documentId);
            var sw = Stopwatch.StartNew();

            var projections = document.Projections.ToDictionary(x => x.Column, x => x.Value);
            byte[] serializedDocument = store.Configuration.Serializer.Serialize(JObject.Parse(document.DocumentAsString));
            projections[document.Table.DocumentColumn] = serializedDocument;
            store.Update(document.Table, document.Id, document.Etag ?? Guid.NewGuid(), projections);
            StatusMessage = string.Format("Saved document {0} in {1}ms", documentId, sw.ElapsedMilliseconds);

            FindDocument(document.Table, document.Id);
            Loading = false;
        }
        
        public void DeleteDocument(DocumentViewModel documentViewModel)
        {
            var document = documentViewModel.Document;

            var result = MessageBox.Show("Are you sure?", "Delete " + document.Name, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            CloseDocument(documentViewModel);
            store.Delete(document.Table, document.Id, document.Etag ?? new Guid());
            StatusMessage = "Deleted document " + document.Name;
        }

        public void FindDocument()
        {
            if (string.IsNullOrWhiteSpace(TableName))
                return;

            var table = new DocumentTable(TableName);

            Guid documentId;
            if (!Guid.TryParse(DocumentId, out documentId))
            {
                MessageBox.Show("Can not parse id as Guid", "Error", MessageBoxButton.OK);
                return;
            }

            var sw = Stopwatch.StartNew();
            StatusMessage = string.Format("Getting document {0} from table {1}", documentId, table.Name);
            Loading = true;
            FindDocument(table, documentId);
            Loading = false;
            StatusMessage = string.Format("Fetched document {0} in {1}ms", documentId, sw.ElapsedMilliseconds);
        }

        void FindDocument(DocumentTable table, Guid documentId)
        {
            IDictionary<Column, object> projections;
            try
            {
                projections = store.Get(table, documentId);
            }
            catch (SqlException e)
            {
                MessageBox.Show(e.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (projections == null)
            {
                MessageBox.Show(string.Format("Could not find document {0} in {1}", documentId, table.Name),
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            var json = (JObject)store.Configuration.Serializer.Deserialize((byte[])projections[table.DocumentColumn], typeof(JObject));
            OpenDocument(new Document(table, json.ToString(), projections));
        }
    }
}