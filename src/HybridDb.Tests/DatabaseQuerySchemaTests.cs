using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit.Extensions;

namespace HybridDb.Tests
{
    public class DatabaseQuerySchemaTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseLocalTempTables)]
        [InlineData(TableMode.UseGlobalTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void ReturnsAllTables(TableMode mode)
        {
            Use(mode);

            store.Execute(new CreateTable(new Table("Entities1", new Column("test", typeof(int)))));
            store.Execute(new CreateTable(new Table("Entities2", new Column("test", typeof(int)))));

            var schema = store.Database.QuerySchema();

            schema.Keys.ShouldContain("Entities1");
            schema.Keys.ShouldContain("Entities2");
        }

        [Theory]
        [InlineData(TableMode.UseLocalTempTables)]
        [InlineData(TableMode.UseGlobalTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void ReturnsAllColumns(TableMode mode)
        {
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