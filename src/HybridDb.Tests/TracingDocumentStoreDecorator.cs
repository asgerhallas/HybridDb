using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Config;

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

        public Guid Execute(IEnumerable<DatabaseCommand> commands)
        {
            commands = commands.ToList();

            foreach (var command in commands)
            {
                if (command is UpdateCommand)
                    Updates.Add((UpdateCommand) command);
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

        public void Dispose()
        {
            store.Dispose();
        }
    }
}