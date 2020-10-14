using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IAdvancedDocumentSession
    {
        IDocumentStore DocumentStore { get; }
        void Defer(DmlCommand command);
        void Evict(object entity);
        void Clear();

        Guid? GetEtagFor(object entity);
        
        Dictionary<string, List<string>> GetMetadataFor(object entity);
        void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata);
        
        IReadOnlyDictionary<EntityKey, ManagedEntity> ManagedEntities { get; }
        bool TryGetManagedEntity<T>(string key, out T entity);
    }
}