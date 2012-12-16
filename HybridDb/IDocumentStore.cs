using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IDocumentStore
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
    }
}