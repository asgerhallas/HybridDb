using System.Collections.Generic;

namespace HybridDb
{
    public interface IHybridDbEvent { }

    public record SavingChanges(
        IDocumentSession Session, 
        IDictionary<ManagedEntity, DmlCommand> DocumentCommands,
        IList<DmlCommand> OtherCommands) : IHybridDbEvent;

    public record AddedToSession(IDocumentSession Session, ManagedEntity ManagedEntity) : IHybridDbEvent;
    public record RemovedFromSession(IDocumentSession Session, ManagedEntity ManagedEntity) : IHybridDbEvent;

    public record MigrationStarted(IDocumentStore Store) : IHybridDbEvent;
    public record MigrationEnded(IDocumentStore Store) : IHybridDbEvent;
}