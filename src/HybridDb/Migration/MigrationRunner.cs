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
            var commands = new SchemaDiffer().CalculateSchemaChanges(store.Schema, store.Configuration);
            foreach (var command in commands)
            {
                command.Execute(store);
            }

            return Task.FromResult(1);
        }
    }
}