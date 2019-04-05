using System.Collections.Generic;

namespace HybridDb.Events
{
    public interface IEventStoreImporter
    {
        void Import(IEnumerable<PreparedCommit> prepares);
    }
}