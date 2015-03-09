using System.Collections.Generic;

namespace HybridDb.Migration
{
    class StaticMigrationProvider : IMigrationProvider
    {
        private readonly Migration[] migrations;

        public StaticMigrationProvider(params Migration[] migrations)
        {
            this.migrations = migrations;
        }

        public IEnumerable<Migration> GetMigrations()
        {
            return migrations;
        }
    }
}