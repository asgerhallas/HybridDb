using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migration;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class MigrationRunnerTests : HybridDbTests
    {
        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            var runner = new MigrationRunner(
                new StaticMigrationProvider(new _001_Something()),
                new FakeSchemaDiffer());

            runner.Migrate(store);

            var tables = store.Schema.GetSchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        public class _001_Something : HybridDb.Migration.Migration
        {
            public _001_Something() : base(1) { }

            public override IEnumerable<MigrationCommand> Migrate()
            {
                yield return new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true))));
                yield return new AddColumn("Testing", new Column("Noget", typeof(int)));
            }
        }

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(ISchema db, Configuration configuration)
            {
                return new List<SchemaMigrationCommand>();
            }
        }
    }

    public class SchemaDifferTests : HybridDbTests
    {
        readonly FakeSchema schema;
        readonly SchemaDiffer migrator;

        public SchemaDifferTests()
        {
            schema = new FakeSchema();
            migrator = new SchemaDiffer();
        }

        [Fact]
        public void FindNewTable()
        {
            configuration.Document<Entity>();

            var command = (CreateTable)migrator.CalculateSchemaChanges(schema, configuration).Single();

            command.Table.ShouldBe(GetTableFor<Entity>());
        }

        [Fact]
        public void FindNewTablesWhenOthersExists()
        {
            schema.CreateTable(new DocumentTable("Entities"));

            configuration.Document<Entity>();
            configuration.Document<OtherEntity>();

            var command = (CreateTable) migrator.CalculateSchemaChanges(schema, configuration).Single();

            command.Table.ShouldBe(GetTableFor<OtherEntity>());
        }

        [Fact]
        public void FindMultipleNewTables()
        {
            configuration.Document<OtherEntity>();
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>();

            var commands = migrator.CalculateSchemaChanges(schema, configuration).Cast<CreateTable>().ToList();

            commands.Count.ShouldBe(2);
            commands[0].Table.ShouldBe(GetTableFor<OtherEntity>());
            commands[1].Table.ShouldBe(GetTableFor<AbstractEntity>());
        }

        [Fact]
        public void FindMissingTables()
        {
            schema.CreateTable(new Table("Entities"));

            var command = (RemoveTable)migrator.CalculateSchemaChanges(schema, configuration).Single();

            command.Tablename.ShouldBe("Entities");
            command.Unsafe.ShouldBe(true);
        }

        [Fact]
        public void FindNewColumn()
        {
            schema.CreateTable(new DocumentTable("Entities"));

            configuration.Document<Entity>()
                .With(x => x.Number);

            var commands = migrator.CalculateSchemaChanges(schema, configuration).Cast<AddColumn>().ToList();

            commands[0].Tablename.ShouldBe("Entities");
            commands[0].Column.ShouldBe(GetTableFor<Entity>()["Number"]);
        }

        [Fact]
        public void FindMissingColumn()
        {
            var table = new DocumentTable("Entities");
            table.Register(new Column("Number", typeof(int)));
            schema.CreateTable(table);

            configuration.Document<Entity>();

            var commands = migrator.CalculateSchemaChanges(schema, configuration).Cast<RemoveColumn>().ToList();

            commands[0].Table.ShouldBe(GetTableFor<Entity>());
            commands[0].Name.ShouldBe("Number");
        }

        [Fact]
        public void FindColumnTypeChange()
        {
            var table = new DocumentTable("Entities");
            table.Register(new Column("Number", typeof(int)));
            schema.CreateTable(table);

            configuration.Document<Entity>()
                .With("Number", x => x.String);

            var commands = migrator.CalculateSchemaChanges(schema, configuration).ToList();

            commands[0].ShouldBeOfType<AddColumn>()
                .Tablename.ShouldBe(GetTableFor<Entity>().Name);
            ((AddColumn)commands[0]).Column.ShouldBe(GetTableFor<Entity>()["Number"]);

            commands[1].ShouldBeOfType<RemoveColumn>()
                .Table.ShouldBe(GetTableFor<Entity>());
            ((RemoveColumn)commands[1]).Name.ShouldBe("Number");
        }

        Table GetTableFor<T>()
        {
            return store.Configuration.GetDesignFor<T>().Table;
        }

        public class FakeSchema : ISchema
        {
            readonly List<Table> tables;

            public FakeSchema()
            {
                tables = new List<Table>();
            }

            public void CreateTable(Table table)
            {
                tables.Add(table);
            }

            public Dictionary<string, Table> GetSchema()
            {
                return tables.ToDictionary(x => x.Name, x => x);
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