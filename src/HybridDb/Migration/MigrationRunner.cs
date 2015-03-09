using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridDb.Migration
{
    public class MigrationRunner
    {
        readonly IMigrationProvider provider;
        private readonly ISchemaDiffer differ;

        public MigrationRunner(IMigrationProvider provider, ISchemaDiffer differ)
        {
            this.provider = provider;
            this.differ = differ;
        }

        public Task Migrate(DocumentStore store)
        {
            var migrations = provider.GetMigrations();

            foreach (var migration in migrations)
            {
                foreach (var command in migration.Migrate())
                {
                    command.Execute(store);
                }
            }

            var commands = differ.CalculateSchemaChanges(store.Schema, store.Configuration);
            foreach (var command in commands)
            {
                if (command.Unsafe)

                command.Execute(store);
            }

            return Task.FromResult(1);
        }
    }
}