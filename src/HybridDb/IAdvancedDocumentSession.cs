using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IAdvancedDocumentSession
    {
        void Clear();
        bool IsLoaded<T>(string key);
        IDocumentStore DocumentStore { get; }
        void Defer(DmlCommand command);
        void Evict(object entity);
        Guid? GetEtagFor(object entity);
        Dictionary<string, List<string>> GetMetadataFor(object entity);
        void SetMetadataFor(object entity, Dictionary<string, List<string>> metadata);
        IReadOnlyDictionary<EntityKey, ManagedEntity> ManagedEntities { get; }
    }
}