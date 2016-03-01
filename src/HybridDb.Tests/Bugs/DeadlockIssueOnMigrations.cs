using System;
using System.Threading.Tasks;
using HybridDb.Config;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class DeadlockIssueOnMigrations : HybridDbTests
    {
        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempTables()
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

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForRealTables()
        {
            UseRealTables();

            Parallel.For(0, 100, i =>
            {
                var realstore = Using(new DocumentStore(new Configuration(), TableMode.UseRealTables, connectionString, true));
                realstore.Configuration.Document<Entity>();
                realstore.Initialize();
            });
        }

        public class Entity
        {
            
        }
    }
}