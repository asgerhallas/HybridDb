using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridDb.Commands;

namespace HybridDb
{
    public interface IAdvancedDocumentSessionCommands
    {
        void Clear();
        bool IsLoaded(string id);
        IDocumentStore DocumentStore { get; }
        void Defer(DatabaseCommand command);
        void Evict(object entity);
        Guid? GetEtagFor(object entity);
        void SaveChanges(bool lastWriteWins, bool forceWriteUnchangedDocument);
        IEnumerable<object> ManagedEntities { get; }
    }
}