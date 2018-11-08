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

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(store.Database);
            new CreateTable(new Table("Entities2", new Column("test", typeof(int)))).Execute(store.Database);

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

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(store.Database);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int))).Execute(store.Database);
            new AddColumn("Entities1", new Column("SomeBool", typeof(bool))).Execute(store.Database);

            new CreateTable(new Table("Entities2", new Column("test", typeof(int)))).Execute(store.Database);
            new AddColumn("Entities2", new Column("SomeString", typeof(string))).Execute(store.Database);

            var schema = store.Database.QuerySchema();

            schema["Entities1"].ShouldContain("SomeInt");
            schema["Entities1"].ShouldContain("SomeBool");
            schema["Entities2"].ShouldContain("SomeString");
        }
    }
}