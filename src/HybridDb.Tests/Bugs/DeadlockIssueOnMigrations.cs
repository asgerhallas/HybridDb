using System;
using System.Threading.Tasks;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class DeadlockIssueOnMigrations : HybridDbTests
    {
        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigations()
        {
            Parallel.For(0, 100, i =>
            {
                var realstore = Using(DocumentStore.ForTesting(TableMode.UseTempTables));
                realstore.Configuration.Document<Entity>();
                realstore.Initialize();
            });
        }

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempDb()
        {
            Parallel.For(0, 100, i =>
            {
                var realstore = Using(DocumentStore.ForTesting(TableMode.UseTempDb));
                realstore.Configuration.UseTableNamePrefix(Guid.NewGuid().ToString());
                realstore.Configuration.Document<Entity>();
                realstore.Initialize();
            });
        }

        [Fact(Skip = "not fully functional")]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForRealTables()
        {
            UseRealTables();

            Parallel.For(0, 100, i =>
            {
                var realstore = Using(new DocumentStore(configuration, TableMode.UseRealTables, connectionString, true));
                realstore.Configuration.Document<Entity>();
                realstore.Initialize();
            });
        }

        public class Entity
        {
            
        }
    }
}