using HybridDb.MigrationRunner;
using Xunit;

namespace HybridDb.Tests
{
    public class MigrationRunnerTests
    {
        [Fact(Skip = "In progress")]
        public void FactMethodName()
        {
            var store = DocumentStore.ForTestingWithTempTables();
            var runner = new Runner(store);
            runner.Migration = (Migration.Migration)new Migration.Migration().MigrateSchema().ToVersion(1).Migrate(migrator => { });
        }
    }
}