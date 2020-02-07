using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations
{
    public class SchemaDifferTests : HybridDbTests
    {
        readonly Dictionary<string, List<string>> schema;
        readonly SchemaDiffer migrator;

        public SchemaDifferTests(ITestOutputHelper output) : base(output)
        {
            schema = new Dictionary<string, List<string>>();
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
            var table = new DocumentTable("Entities");

            schema.Add(table.Name, table.Columns.Select(x => x.Name).ToList());

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
            commands.ShouldContain(x => x.Table == GetTableFor<OtherEntity>());
            commands.ShouldContain(x => x.Table == GetTableFor<AbstractEntity>());
        }

        [Fact]
        public void FindMissingTables()
        {
            schema.Add("Entities", new List<string>());

            var command = (RemoveTable)migrator.CalculateSchemaChanges(schema, configuration).Single();

            command.Tablename.ShouldBe("Entities");
            command.Safe.ShouldBe(false);
        }

        [Fact]
        public void FindNewColumn()
        {
            var table = new DocumentTable("Entities");

            schema.Add(table.Name, table.Columns.Select(x => x.Name).ToList());

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
            table.Add(new Column<int>("Number"));

            schema.Add(table.Name, table.Columns.Select(x => x.Name).ToList());

            configuration.Document<Entity>();

            var commands = migrator.CalculateSchemaChanges(schema, configuration).Cast<RemoveColumn>().ToList();

            commands[0].Table.ShouldBe(GetTableFor<Entity>());
            commands[0].Name.ShouldBe("Number");
        }

        [Fact(Skip = "Not yet supported")]
        public void FindColumnTypeChange()
        {
            var table = new DocumentTable("Entities");
            table.Add(new Column<int>("Number"));

            schema.Add(table.Name, table.Columns.Select(x => x.Name).ToList());

            configuration.Document<Entity>().With("Number", x => x.String);

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
            return configuration.GetDesignFor<T>().Table;
        }

        public class FakeSchema
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