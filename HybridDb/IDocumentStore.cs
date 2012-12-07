using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IDocumentStore
    {
        void Initialize();
        IDocumentSession OpenSession();
        Schema Schema { get; }
        TableConfiguration<TEntity> ForDocument<TEntity>();
        void Insert<T>(Guid id, Guid etag, T values);
        void Insert(ITableConfiguration table, object values);
        void Update(ITableConfiguration table, object values);
        IDictionary<string, object> Get(ITableConfiguration table, Guid id);
    }
}