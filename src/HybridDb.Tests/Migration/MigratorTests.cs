using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using HybridDb.Migration.Commands;
using HybridDb.Schema;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class MigratorTests : HybridDbTests
    {
        readonly DocumentStoreMigrator migrator;

        public MigratorTests()
        {
            migrator = new DocumentStoreMigrator();
        }

        [Fact]
        public void FindNewTable()
        {
            EnsureStoreInitialized();

            store.Configuration.Document<Entity>();

            var command = (CreateTable)migrator.FindSchemaChanges(store).Single();

            command.Table.ShouldBe(GetTableFor<Entity>());
        }

        [Fact]
        public void FindNewTablesWhenOthersExists()
        {
            Document<Entity>();

            EnsureStoreInitialized();

            store.Configuration.Document<OtherEntity>();

            var command = (CreateTable) migrator.FindSchemaChanges(store).Single();

            command.Table.ShouldBe(GetTableFor<OtherEntity>());
        }

        [Fact]
        public void FindMultipleNewTables()
        {
            Document<Entity>();

            EnsureStoreInitialized();

            store.Configuration.Document<OtherEntity>();
            store.Configuration.Document<AbstractEntity>();
            store.Configuration.Document<MoreDerivedEntity1>();

            var commands = migrator.FindSchemaChanges(store).Cast<CreateTable>().ToList();

            commands.Count.ShouldBe(2);
            commands[0].Table.ShouldBe(GetTableFor<OtherEntity>());
            commands[1].Table.ShouldBe(GetTableFor<AbstractEntity>());
        }

        [Fact]
        public void FindMissingTables()
        {
            UseRealTables();

            new CreateTable(new DocumentTable("Entity"));

            Document<Entity>();

            EnsureStoreInitialized();

            configuration = new Configuration();

            var command = (RemoveTable)migrator.FindSchemaChanges(store).Single();

            command.Tablename.ShouldBe("Entities");
            command.Unsafe.ShouldBe(true);
        }

        [Fact]
        public void FindsSchemaMigrations()
        {
            //new Migrator().Run(new AddTable())
        }


        Table GetTableFor<T>()
        {
            return store.Configuration.GetDesignFor<T>().Table;
        }

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
            public int Number { get; set; }
        }
    }
}