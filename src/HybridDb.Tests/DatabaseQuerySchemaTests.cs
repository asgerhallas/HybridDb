using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests
{
    public class DatabaseQuerySchemaTests : HybridDbTests
    {
        [Fact]
        public void ReturnsAllTables()
        {
            Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            Execute(new CreateTable(new Table("Entities2", new Column("test", typeof(int)))));

            var schema = store.Database.QuerySchema();

            schema.Keys.ShouldContain("Entities1");
            schema.Keys.ShouldContain("Entities2");
        }

        [Fact]
        public void ReturnsAllColumns()
        {
            Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            Execute(new AddColumn("Entities1", new Column("SomeInt", typeof(int))));
            Execute(new AddColumn("Entities1", new Column("SomeBool", typeof(bool))));

            Execute(new CreateTable(new Table("Entities2", new Column("test", typeof(int)))));
            Execute(new AddColumn("Entities2", new Column("SomeString", typeof(string))));

            var schema = store.Database.QuerySchema();

            schema["Entities1"].ShouldContain("SomeInt");
            schema["Entities1"].ShouldContain("SomeBool");
            schema["Entities2"].ShouldContain("SomeString");
        }
    }
}