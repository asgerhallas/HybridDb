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
        void Insert(ITableConfiguration table, object values);
        void Update(ITableConfiguration table, object values);
        Dictionary<IColumnConfiguration, object> Get(ITableConfiguration table, Guid id, Guid? etag);
    }

    public class ConcurrencyException : Exception
    {
    }
}