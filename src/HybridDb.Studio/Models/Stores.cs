using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Studio.Models
{
    public class Stores : IEnumerable<Store>
    {
        readonly List<Store> stores;

        public Stores()
        {
            stores = new List<Store>();
        }
        
        public Store NewStore(string connectionString, IHybridDbConfigurator configurator)
        {
            var documentStore = DocumentStore.Create(connectionString, configurator);

            var tables = documentStore.Configuration.DocumentDesigns.Select(x => new Table(x.Table)).ToList();

            var store = new Store(documentStore, tables);
            stores.Add(store);

            return store;
        }

        public IEnumerator<Store> GetEnumerator()
        {
            return stores.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) stores).GetEnumerator();
        }
    }
}