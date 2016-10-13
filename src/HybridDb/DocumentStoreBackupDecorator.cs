using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;
using HybridDb.Migrations;

namespace HybridDb
{
    public class DocumentStoreBackupDecorator : IDocumentStore
    {
        readonly IDocumentStore store;
        readonly IBackupWriter writer;

        public DocumentStoreBackupDecorator(IDocumentStore store, IBackupWriter writer)
        {
            this.store = store;
            this.writer = writer;
        }

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
            //todo:
            foreach (var command in commands.OfType<BackupCommand>())
            {
                writer.Write($"{command.Design.DocumentType.FullName}_{command.Key}_{command.Version}.bak", command.OldDocument);
            }

            return store.Execute(commands);
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            return store.Get(table, key);
        }

        public IEnumerable<TProjection> Query<TProjection>(
            DocumentTable table, out QueryStats stats, string @select = "", string @where = "", 
            int skip = 0, int take = 0, string @orderby = "", object parameters = null)
        {
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