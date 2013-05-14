using System;
using System.Collections.Generic;
using HybridDb.Commands;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        IMigrator CreateMigrator();
        void InitializeDatabase(bool safe = true);
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }
        IDocumentSession OpenSession();
        TableBuilder<TEntity> DocumentsFor<TEntity>();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(Table table, Guid key, byte[] document, object projections);
        Guid Update(Table table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins = false);
        void Delete(Table table, Guid key, Guid etag, bool lastWriteWins = false);
        IDictionary<Column, object> Get(Table table, Guid key);
        IEnumerable<IDictionary<Column, object>> Query(Table table, out QueryStats stats, string select = "", string where = "",
                                                        int skip = 0, int take = 0, string orderby = "", object parameters = null);

        IEnumerable<TProjection> Query<TProjection>(Table table, out QueryStats stats, string select = "", string where = "",
                                                    int skip = 0, int take = 0, string orderby = "", object parameters = null);
    }
}