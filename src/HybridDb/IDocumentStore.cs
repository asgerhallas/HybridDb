using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq2.Ast;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        bool IsInitialized { get; }

        void Initialize();
        IDocumentSession OpenSession();
        Guid Execute(IReadOnlyList<DatabaseCommand> commands);
        IDictionary<string, object> Get(DocumentTable table, string key);
        IEnumerable<TProjection> Query<TProjection>(DocumentTable table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
        IEnumerable<TProjection> Query<TProjection>(SelectStatement statement, out QueryStats stats);
    }

}