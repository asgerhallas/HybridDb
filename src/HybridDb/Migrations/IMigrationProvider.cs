using System.Collections.Generic;

namespace HybridDb.Migrations
{
    public interface IMigrationProvider
    {
        IReadOnlyList<Migration> GetMigrations();
        int CurrentVersion { get; }
    }
}