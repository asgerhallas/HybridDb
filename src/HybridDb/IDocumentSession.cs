using System;
using System.Linq;
using HybridDb.Events;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(string key) where T : class;
        object Load(Type type, string key);
        IQueryable<T> Query<T>() where T : class;
        void Store(object entity);
        void Store(string key, object entity);
        void Append(int generation, EventData<byte[]> @event);
        void Delete(object entity);
        
        Guid SaveChanges();
        Guid SaveChanges(DocumentTransaction tx);
        Guid SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        Guid SaveChanges(DocumentTransaction tx, bool lastWriteWins, bool forceWriteUnchangedDocument);
        
        IAdvancedDocumentSession Advanced { get; }
    }
}