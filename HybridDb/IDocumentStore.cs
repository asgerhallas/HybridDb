using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HybridDb.Commands;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        void Initialize();
        IDocumentSession OpenSession();
        Configuration Configuration { get; }
        Table<TEntity> ForDocument<TEntity>();
        Guid Execute(params DatabaseCommand[] commands);
        Guid Insert(ITable table, Guid key, byte[] document, object projections);
        Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections);
        IDictionary<IColumn, object> Get(ITable table, Guid key);
        void Delete(ITable table, Guid key, Guid etag);
        long NumberOfRequests { get; }
        Guid LastWrittenEtag { get; }

        IEnumerable<IDictionary<IColumn, object>> Query(ITable table, out long totalRows, string columns = "*", string @where = "", int skip = 0, int take = 0,
                                                        string orderby = "", object parameters = null);

        IEnumerable<TProjection> Query<TProjection>(ITable table, out long totalRows, string columns = "*", string @where = "", int skip = 0, int take = 0,
                                                    string orderby = "", object parameters = null);
    }
}