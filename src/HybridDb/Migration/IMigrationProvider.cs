using System.Collections.Generic;

namespace HybridDb.Migration
{
    public interface IMigrationProvider
    {
        IEnumerable<Migration> GetMigrations();
    }
}