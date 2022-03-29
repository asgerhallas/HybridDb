using System;
using System.Collections.Generic;
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

    public interface IAdvancedDocumentSession
    {
        IDocumentStore DocumentStore { get; }
        DocumentTransaction DocumentTransaction { get; }
        IReadOnlyList<DmlCommand> DeferredCommands { get; }

        void Defer(DmlCommand command);
        void Enlist(DocumentTransaction tx);
        void Evict(object entity);
        void Clear();

        Guid? GetEtagFor(object entity);
        bool Exists<T>(string key, out Guid? etag) where T : class;
        bool Exists(Type type, string key, out Guid? etag);

        Dictionary<string, List<string>> GetMetadataFor(object entity);
        void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata);

        IReadOnlyDictionary<EntityKey, ManagedEntity> ManagedEntities { get; }
        bool TryGetManagedEntity<T>(string key, out T entity);

        Dictionary<object, object> SessionData { get; }
    }
}