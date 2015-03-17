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
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(DocumentTable table, Guid key, object projections);
        Guid Update(DocumentTable table, Guid key, Guid etag, object projections, bool lastWriteWins = false);
        void Delete(DocumentTable table, Guid key, Guid etag, bool lastWriteWins = false);
        IDictionary<string, object> Get(DocumentTable table, Guid key);
        IEnumerable<IDictionary<string, object>> Query(DocumentTable table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
        IEnumerable<TProjection> Query<TProjection>(DocumentTable table, out QueryStats stats, string select = "", string where = "", int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}