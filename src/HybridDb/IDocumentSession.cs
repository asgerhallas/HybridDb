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
        void Store(object entity, Guid? etag);
        void Store(string key, object entity);
        void Store(string key, object entity, Guid? etag);

        void Delete(object entity);
        void Append(int generation, EventData<byte[]> @event);
        
        Guid SaveChanges();
        Guid SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        
        IAdvancedDocumentSession Advanced { get; }
    }
}