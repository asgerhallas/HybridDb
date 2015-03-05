using System.Collections.Generic;

namespace HybridDb.Studio.Models
{
    public class Store
    {
        public Store(IDocumentStore documentStore, IEnumerable<Table> tables)
        {
            Tables = tables;
            DocumentStore = documentStore;
        }

        public IDocumentStore DocumentStore { get; private set; }
        public IEnumerable<Table> Tables { get; private set; }
    }
}