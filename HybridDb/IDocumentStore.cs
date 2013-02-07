using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        IMigration Migration { get; }
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        IDocumentSession OpenSession();
        TableBuilder<TEntity> DocumentsFor<TEntity>();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(ITable table, Guid key, byte[] document, object projections);
        Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins = false);
        void Delete(ITable table, Guid key, Guid etag, bool lastWriteWins = false);
        IDictionary<Column, object> Get(ITable table, Guid key);
        IEnumerable<IDictionary<Column, object>> Query(ITable table, out QueryStats stats, string select = "", string where = "",
                                                        int skip = 0, int take = 0, string orderby = "", object parameters = null);

        IEnumerable<TProjection> Query<TProjection>(ITable table, out QueryStats stats, string select = "", string where = "",
                                                    int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}