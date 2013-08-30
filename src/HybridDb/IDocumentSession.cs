using System;
using System.Linq;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(Guid id) where T : class;
        T Load<T, TIndex>(Guid id) where T : class;
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Delete(object entity);
        void SaveChanges();
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}