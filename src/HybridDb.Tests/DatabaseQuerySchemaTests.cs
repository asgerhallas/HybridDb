using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DatabaseQuerySchemaTests : HybridDbTests
    {
        public DatabaseQuerySchemaTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void ReturnsAllTables(TableMode mode)
        {
            NoInitialize();
            Use(mode);

            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            store.Execute(new CreateTable(new Table("Entities2", new Column("test", typeof(int)))));

            var schema = store.Database.QuerySchema();

            schema.Keys.ShouldContain("Entities1");
            schema.Keys.ShouldContain("Entities2");
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void ReturnsAllColumns(TableMode mode)
        {
            NoInitialize();
            Use(mode);

            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            store.Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int))));
            store.Execute(new AddColumn("Entities1", new Column("SomeBool", typeof(bool))));

            store.Execute(new CreateTable(new Table("Entities2", new Column("test", typeof(int)))));
            store.Execute(new AddColumn("Entities2", new Column("SomeString", typeof(string))));

            var schema = store.Database.QuerySchema();

            schema["Entities1"].ShouldContain("SomeInt");
            schema["Entities1"].ShouldContain("SomeBool");
            schema["Entities2"].ShouldContain("SomeString");
        }
    }
}