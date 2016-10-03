using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;
using Column = HybridDb.Config.Column;

namespace HybridDb
{
    public class InMemoryDocumentStore : IDocumentStore
    {
        readonly ConcurrentDictionary<Table, ConcurrentDictionary<Column, object>> data = 
            new ConcurrentDictionary<Table, ConcurrentDictionary<Column, object>>();

        public InMemoryDocumentStore(Configuration configuration)
        {
            Configuration = configuration;
        }

        public Configuration Configuration { get; }
        public long NumberOfRequests { get; }
        public Guid LastWrittenEtag { get; }

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public IDocumentSession OpenSession()
        {
            return new DocumentSession(this);
        }

        public Guid Execute(IReadOnlyList<DatabaseCommand> commands)
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TProjection> Query<TProjection>(DocumentTable table, out QueryStats stats, string @select = "", string @where = "", int skip = 0, int take = 0, string @orderby = "",
            object parameters = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TProjection> Query<TProjection>(SelectStatement @select, out QueryStats stats)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}