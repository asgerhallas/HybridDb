using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        
        IDocumentSession OpenSession();
        Guid Execute(IEnumerable<DatabaseCommand> commands);
        IDictionary<string, object> Get(DocumentTable table, string key);
        IEnumerable<TProjection> Query<TProjection>(DocumentTable table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}