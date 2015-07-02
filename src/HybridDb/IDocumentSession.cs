using System;
using System.Linq;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(string key) where T : class;
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Delete(object entity);
        void SaveChanges();
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}