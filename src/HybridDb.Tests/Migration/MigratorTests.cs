using System.Collections.Generic;
using System.Linq;
using HybridDb.Migration.Commands;
using HybridDb.Schema;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class MigratorTests : HybridDbTests
    {
        readonly FakeDatabase db;
        readonly DocumentStoreMigrator migrator;

        public MigratorTests()
        {
            db = new FakeDatabase();
            migrator = new DocumentStoreMigrator();
        }

        [Fact]
        public void FindNewTable()
        {
            configuration.Document<Entity>();

            var command = (CreateTable)migrator.FindSchemaChanges(db, configuration).Single();

            command.Table.ShouldBe(GetTableFor<Entity>());
        }

        [Fact]
        public void FindNewTablesWhenOthersExists()
        {
            db.CreateTable("Entities");

            configuration.Document<Entity>();
            configuration.Document<OtherEntity>();

            var command = (CreateTable) migrator.FindSchemaChanges(db, configuration).Single();

            command.Table.ShouldBe(GetTableFor<OtherEntity>());
        }

        [Fact]
        public void FindMultipleNewTables()
        {
            configuration.Document<OtherEntity>();
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>();

            var commands = migrator.FindSchemaChanges(db, configuration).Cast<CreateTable>().ToList();

            commands.Count.ShouldBe(2);
            commands[0].Table.ShouldBe(GetTableFor<OtherEntity>());
            commands[1].Table.ShouldBe(GetTableFor<AbstractEntity>());
        }

        [Fact]
        public void FindMissingTables()
        {
            db.CreateTable("Entities");

            var command = (RemoveTable)migrator.FindSchemaChanges(db, configuration).Single();

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

        public class FakeDatabase : IDatabase
        {
            readonly Dictionary<string, List<string>> tables;

            public FakeDatabase()
            {
                tables = new Dictionary<string, List<string>>();
            }

            public void CreateTable(string name)
            {
                tables.Add(name, new List<string>());
            }

            public bool TableExists(string name)
            {
                return tables.ContainsKey(name);
            }

            public List<string> GetTables()
            {
                return tables.Keys.ToList();
            }
        }

        public class Entity
        {
            public string String { get; set; }
            public List<string> Strings { get; set; }
            public int Number { get; set; }
        }
    }
}