using System;
using System.Collections.Generic;
using System.Data;
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
        IDocumentSession OpenSession(IDocumentTransaction tx = null);
        IDocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    }

    public interface IDocumentTransaction : IDisposable
    {
        IDocumentStore Store { get; }

        Guid Execute(DatabaseCommand command);
        IDictionary<string, object> Get(DocumentTable table, string key);
        (QueryStats stats, IEnumerable<QueryResult<TProjection>> rows) Query<TProjection>(
            DocumentTable table, string select = "", string where = "", int skip = 0, int take = 0,
            string orderby = "", bool includeDeleted = false, object parameters = null);

        Guid Complete();
    }
}