using System;
using HybridDb.Commands;

namespace HybridDb
{
    public interface IAdvancedDocumentSessionCommands
    {
        void Clear();
        bool IsLoaded(Guid id);
        IDocumentStore DocumentStore { get; }
        void Defer(DatabaseCommand command);
        void Evict(object entity);
        Guid? GetEtagFor(object entity);
        void SaveChangesLastWriterWins();
    }
}