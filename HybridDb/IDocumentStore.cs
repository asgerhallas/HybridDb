using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        void Initialize();
        IDocumentSession OpenSession();
        Table<TEntity> ForDocument<TEntity>();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(ITable table, Guid key, byte[] document, object projections);
        Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins = false);
        IDictionary<IColumn, object> Get(ITable table, Guid key);
        void Delete(ITable table, Guid key, Guid etag, bool lastWriteWins = false);

        IEnumerable<IDictionary<IColumn, object>> Query(ITable table, out QueryStats stats, string select = "", string where = "",
                                                        int skip = 0, int take = 0, string orderby = "", object parameters = null);

        IEnumerable<TProjection> Query<TProjection>(ITable table, out QueryStats stats, string select = "", string where = "",
                                                    int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}