using System;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class QueryWithIdTypeMismatch : HybridDbTests
    {
        [Fact(Skip="Skipped until chnage col type migration is done")]
        public void FactMethodName()
        {
            new CreateTable(new Table("Entities", new Column("Id", typeof (Guid), isPrimaryKey: true))).Execute(store.Database);

            store.Configuration.Document<EntityWithGuidKey>("Entities");
            store.Initialize();

            var session = store.OpenSession();
            session.Store(new EntityWithGuidKey
            {
                Id = Guid.NewGuid()
            });
            session.SaveChanges();
            session.Advanced.Clear();

            session.Query<EntityWithGuidKey>().ToList().Count.ShouldBe(1);
        }

        public class EntityWithGuidKey
        {
            public Guid Id { get; set; }
        }

    }

    public class DeadlockIssueOnMigrations : HybridDbTests
    {
        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempTables()
        {
            Parallel.For(0, 100, i =>
            {
                var realstore = Using(DocumentStore.ForTesting(TableMode.UseTempTables, connectionString));
                realstore.Configuration.Document<Entity>();
                realstore.Initialize();
            });
        }

        [Fact]
        public void ShouldTryNotToDeadlockOnSchemaMigationsForTempDb()
        {
            Parallel.For(0, 100, i =>
            {
                var realstore = Using(DocumentStore.ForTesting(TableMode.UseTempDb, connectionString));
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