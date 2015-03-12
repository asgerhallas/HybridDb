using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Migrations
{
    class StaticMigrationProvider : IMigrationProvider
    {
        private readonly Migration[] migrations;

        public StaticMigrationProvider(params Migration[] migrations)
        {
            this.migrations = migrations;
        }

        public IReadOnlyList<Migration> GetMigrations()
        {
            return migrations;
        }

        public int CurrentVersion
        {
            get { return migrations.Any() ? migrations.Max(x => x.Version) : 0; }
        }
    }
}