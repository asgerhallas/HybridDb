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
        Guid Insert(ITable table, Guid key, byte[] document, object projections);
        Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections);
        Document Get(ITable table, Guid key);
    }

    public class Document
    {
        public Guid Etag { get; set; }
        public IDictionary<string, object> Projections { get; set; }
        public byte[] Data { get; set; }
    }
}