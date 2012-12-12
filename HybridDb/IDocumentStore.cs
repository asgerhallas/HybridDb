using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IDocumentStore
    {
        void Initialize();
        IDocumentSession OpenSession();
        Schema Schema { get; }
        Table<TEntity> ForDocument<TEntity>();
        void Insert(Guid key, object projections, byte[] document);
        void Update(Guid key, Guid etag, object projections, byte[] document);
        IDictionary<string, object> Get(ITable table, Guid key);
    }
}