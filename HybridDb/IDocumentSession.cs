using System;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(Guid id) where T : class;
        void Store(object entity);
        void Delete(object entity);
        void SaveChanges();
        IAdvancedDocumentSessionCommands Advanced { get; }
    }
}