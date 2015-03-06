using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migration;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class MigratorTests : HybridDbTests
    {
        readonly FakeSchema schema;
        readonly DocumentStoreMigrator migrator;

        public MigratorTests()
        {
            schema = new FakeSchema();
            migrator = new DocumentStoreMigrator();
        }

        [Fact]
        public void FindNewTable()
        {
            configuration.Document<Entity>();

            var command = (CreateTable)migrator.FindSchemaChanges(schema, configuration).Single();

            command.Table.ShouldBe(GetTableFor<Entity>());
        }

        [Fact]
        public void FindNewTablesWhenOthersExists()
        {
            schema.CreateTable(new DocumentTable("Entities"));

            configuration.Document<Entity>();
            configuration.Document<OtherEntity>();

            var command = (CreateTable) migrator.FindSchemaChanges(schema, configuration).Single();

            command.Table.ShouldBe(GetTableFor<OtherEntity>());
        }

        [Fact]
        public void FindMultipleNewTables()
        {
            configuration.Document<OtherEntity>();
            configuration.Document<AbstractEntity>();
            configuration.Document<MoreDerivedEntity1>();

            var commands = migrator.FindSchemaChanges(schema, configuration).Cast<CreateTable>().ToList();

            commands.Count.ShouldBe(2);
            commands[0].Table.ShouldBe(GetTableFor<OtherEntity>());
            commands[1].Table.ShouldBe(GetTableFor<AbstractEntity>());
        }

        [Fact]
        public void FindMissingTables()
        {
            schema.CreateTable(new Table("Entities"));

            var command = (RemoveTable)migrator.FindSchemaChanges(schema, configuration).Single();

            command.Tablename.ShouldBe("Entities");
            command.Unsafe.ShouldBe(true);
        }

        [Fact]
        public void FindNewColumn()
        {
            schema.CreateTable(new DocumentTable("Entities"));

            configuration.Document<Entity>()
                .With(x => x.Number);

            var commands = migrator.FindSchemaChanges(schema, configuration).Cast<AddColumn>().ToList();

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

            var commands = migrator.FindSchemaChanges(schema, configuration).Cast<RemoveColumn>().ToList();

            commands[0].Tablename.ShouldBe("Entities");
            commands[0].Column.ShouldBe(GetTableFor<Entity>()["Number"]);
        }

        [Fact]
        public void FindColumnTypeChange()
        {
            
        }

        Table GetTableFor<T>()
        {
            return store.Configuration.GetDesignFor<T>().Table;
        }

        public class FakeSchema : ISchema
        {
            readonly Dictionary<string, Table> tables;

            public FakeSchema()
            {
                tables = new Dictionary<string, Table>();
            }

            public void CreateTable(Table table)
            {
                tables.Add(table.Name, table);
            }

            public bool TableExists(string name)
            {
                return tables.ContainsKey(name);
            }

            public List<string> GetTables()
            {
                return tables.Keys.ToList();
            }

            public Column GetColumn(string tablename, string columnname)
            {
                return tables[tablename].Columns.SingleOrDefault(x => x.Name == columnname);
            }

            public string GetType(int id)
            {
                return null;
            }

            public bool IsPrimaryKey(string column)
            {
                throw new System.NotImplementedException();
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