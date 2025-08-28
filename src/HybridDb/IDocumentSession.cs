using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Events;

namespace HybridDb
{
    public interface IDocumentSession : IDisposable
    {
        T Load<T>(string key, bool readOnly = false) where T : class;
        object Load(Type requestedType, string key, bool readOnly = false);

        IReadOnlyList<T> Load<T>(IReadOnlyList<string> keys, bool readOnly = false) where T : class;
        IReadOnlyList<object> Load(Type requestedType, IReadOnlyList<string> keys, bool readOnly = false);

        IQueryable<T> Query<T>() where T : class;

        IEnumerable<T> Query<T>(SqlBuilder sql);

        T Store<T>(T entity) where T: class;
        T Store<T>(T entity, Guid? etag) where T : class;
        T Store<T>(string key, T entity) where T : class;
        T Store<T>(string key, T entity, Guid? etag) where T : class;

        void Delete(object entity);
        void Append(int generation, EventData<byte[]> @event);
        
        Guid SaveChanges();
        Guid SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        
        IAdvancedDocumentSession Advanced { get; }
        Guid CommitId { get; }
    }

    public interface IAdvancedDocumentSession
    {
        IDocumentStore DocumentStore { get; }
        DocumentTransaction DocumentTransaction { get; }

        void Defer(HybridDbCommand command);
        void Enlist(DocumentTransaction tx);
        void Evict(object entity);
        void Clear();

        IDocumentSession Copy();

        Guid? GetEtagFor(object entity);
        bool Exists<T>(string key, out Guid? etag) where T : class;
        bool Exists(Type type, string key, out Guid? etag);

        Dictionary<string, List<string>> GetMetadataFor(object entity);
        void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata);

        ManagedEntities ManagedEntities { get; }
        bool TryGetManagedEntity<T>(string key, out T entity);

        Dictionary<object, object> SessionData { get; }
        IReadOnlyList<HybridDbCommand> DeferredCommands { get; }
        IReadOnlyList<(int Generation, EventData<byte[]> Data)> Events { get; }
    }
}