using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;
using HybridDb.Config;

namespace HybridDb
{
    public interface IAdvancedDocumentSessionCommands
    {
        void Clear();
        bool IsLoaded(string id);
        IDocumentStore DocumentStore { get; }
        object Load(DocumentDesign design, string key);
        void Defer(DatabaseCommand command);
        void Evict(object entity);
        Guid? GetEtagFor(object entity);
        void SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        IEnumerable<object> ManagedEntities { get; }
    }
}