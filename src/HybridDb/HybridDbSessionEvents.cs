using System.Collections.Generic;

namespace HybridDb
{
    public interface IHybridDbSessionEvents { }

    record SavingChanges(
        IDocumentSession Session, 
        IDictionary<ManagedEntity, DmlCommand> DocumentCommands,
        IList<DmlCommand> OtherCommands) : IHybridDbSessionEvents;
}