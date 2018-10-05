using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        StoreStats Stats { get; }

        bool IsInitialized { get; }
        bool Testing { get; }
        TableMode TableMode { get; }

        void Initialize();
        IDocumentSession OpenSession();
        IDocumentTransaction BeginTransaction();
    }

    public interface IDocumentTransaction : IDisposable
    {
        Guid Execute(DatabaseCommand command);
        IDictionary<string, object> Get(DocumentTable table, string key);
        IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            DocumentTable table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0,
            string orderby = "", bool includeDeleted = false, object parameters = null);

        Guid Complete();
    }
}