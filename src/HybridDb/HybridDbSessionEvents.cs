using System.Collections.Generic;

namespace HybridDb
{
    public interface IHybridDbSessionEvents { }

    public record SavingChanges(
        IDocumentSession Session, 
        IDictionary<ManagedEntity, DmlCommand> DocumentCommands,
        IList<DmlCommand> OtherCommands) : IHybridDbSessionEvents;
}