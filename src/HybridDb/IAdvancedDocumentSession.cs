using System;
using System.Collections.Generic;
using HybridDb.Commands;

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
        IEnumerable<ManagedEntity> ManagedEntities { get; }
    }
}