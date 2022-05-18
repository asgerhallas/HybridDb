using System;
using System.Collections.Generic;

namespace HybridDb
{
    public interface IHybridDbEvent { }

    public record SaveChanges_BeforePrepareCommands(IDocumentSession Session) : IHybridDbEvent;

    public record SaveChanges_BeforeExecuteCommands(
        IDocumentSession Session, 
        IDictionary<ManagedEntity, DmlCommand> DocumentCommands,
        IList<DmlCommand> OtherCommands) : IHybridDbEvent;

    public record EntityLoaded(IDocumentSession Session, Type RequestedType, ManagedEntity ManagedEntity) : IHybridDbEvent;

    public record AddedToSession(IDocumentSession Session, ManagedEntity ManagedEntity) : IHybridDbEvent;
    public record RemovedFromSession(IDocumentSession Session, ManagedEntity ManagedEntity) : IHybridDbEvent;

    public record MigrationStarted(IDocumentStore Store) : IHybridDbEvent;
    public record MigrationEnded(IDocumentStore Store) : IHybridDbEvent;
}