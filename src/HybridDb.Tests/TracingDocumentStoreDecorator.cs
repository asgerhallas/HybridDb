using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;

namespace HybridDb.Tests
{
    public class TracingDocumentStoreDecorator : IDocumentStore
    {
        readonly IDocumentStore store;

        public TracingDocumentStoreDecorator(IDocumentStore store)
        {
            this.store = store;

            Gets = new List<Tuple<DocumentTable, string>>();
            Queries = new List<DocumentTable>();
            Updates = new List<UpdateCommand>();
        }

        public List<Tuple<DocumentTable, string>> Gets { get; private set; }
        public List<DocumentTable> Queries { get; private set; }
        public List<UpdateCommand> Updates { get; private set; }

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

        public Guid Execute(IReadOnlyList<DatabaseCommand> commands)
        {
            foreach (var command in commands.OfType<UpdateCommand>())
            {
                Updates.Add(command);
            }

            return store.Execute(commands);
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            Gets.Add(Tuple.Create(table, key));
            return store.Get(table, key);
        }

        public IEnumerable<TProjection> Query<TProjection>(
            DocumentTable table, out QueryStats stats, string @select = "", string @where = "", int skip = 0, int take = 0, string @orderby = "", object parameters = null)
        {
            Queries.Add(table);
            return store.Query<TProjection>(table, out stats, select, where, skip, take, orderby, parameters);
        }

        public IEnumerable<TProjection> Query<TProjection>(SelectStatement statement, out QueryStats stats)
        {
            return store.Query<TProjection>(statement, out stats);
        }

        public void Dispose()
        {
            store.Dispose();
        }
    }
}