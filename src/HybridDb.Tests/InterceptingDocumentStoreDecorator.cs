using System;
using System.Collections;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb.Tests
{
    public class InterceptingDocumentStoreDecorator : IDocumentStore
    {
        readonly IDocumentStore store;

        public InterceptingDocumentStoreDecorator(IDocumentStore store)
        {
            this.store = store;

            OverrideExecute = (s, args) => s.Execute(args);
        }

        public Func<IDocumentStore, IEnumerable<DatabaseCommand>, Guid> OverrideExecute { get; set; } 

        public Configuration Configuration => store.Configuration;
        public long NumberOfRequests => store.NumberOfRequests;
        public Guid LastWrittenEtag => store.LastWrittenEtag;
        public bool IsInitialized => store.IsInitialized;

        public void Initialize()
        {
            store.Initialize();
        }

        public IDocumentSession OpenSession()
        {
            return new DocumentSession(this);
        }

        public Guid Execute(IEnumerable<DatabaseCommand> commands)
        {
            return OverrideExecute(store, commands);
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            return store.Get(table, key);
        }

        public IEnumerable<QueryResult<TProjection>>  Query<TProjection>(
            DocumentTable table, out QueryStats stats, string @select = "", string @where = "",
            int skip = 0, int take = 0, string @orderby = "", bool includeDeleted = false, object parameters = null)
        {
            return store.Query<TProjection>(table, out stats, select, where, skip, take, orderby, includeDeleted, parameters);
        }

        public void Dispose()
        {
            store.Dispose();
        }
    }
}