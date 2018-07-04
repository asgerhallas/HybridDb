using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HybridDb.Commands;
using HybridDb.Config;

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
        Task<Guid> Execute(IEnumerable<DatabaseCommand> commands);
        Task<IDictionary<string, object>> Get(DocumentTable table, string key);
        IEnumerable<QueryResult<TProjection>> Query<TProjection>(
            DocumentTable table, out QueryStats stats, string select = "", 
            string where = "", int skip = 0, int take = 0, 
            string orderby = "", bool includeDeleted = false, object parameters = null);
    }
}