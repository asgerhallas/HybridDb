using System;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(string id);
        void Store(object entity);
        void Delete(object entity);
        void SaveChanges();
    }
}