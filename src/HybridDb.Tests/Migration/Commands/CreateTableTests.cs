using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class CreateTableTests : HybridDbStoreTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesTable(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(database);

            database.QuerySchema().ShouldContainKey("Entities");
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesColumns(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(database);

            database.QuerySchema().ShouldContainKey("Entities");
            database.QuerySchema()["Entities"]["Col1"].Type.ShouldBe(typeof(string));
        }

        [Theory]
        [InlineData(TableMode.UseTempTables, typeof(int), null)]
        [InlineData(TableMode.UseRealTables, typeof(int), null)]
        [InlineData(TableMode.UseTempTables, typeof(string), 255)]
        [InlineData(TableMode.UseRealTables, typeof(string), 255)]
        public void CreatesPrimaryKeyColumn(TableMode mode, Type type, int? length)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", type, length, isPrimaryKey: true))).Execute(database);

            database.QuerySchema()["Entities"]["Col1"].IsPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            Should.NotThrow(() => new CreateTable(new Table("Create",new Column("By Int", typeof(int)))));
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CanCreateColumnWithDefaultValue(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1",
                new Column("SomeNullableInt", typeof(int?), defaultValue: null),
                new Column("SomeOtherNullableInt", typeof(int?), defaultValue: 42),
                new Column("SomeString", typeof(string),  defaultValue: "peter"),
                new Column("SomeInt", typeof(int),  defaultValue: 666),
                new Column("SomeDateTime", typeof(DateTime),  defaultValue: new DateTime(1999, 12, 24)))).Execute(database);

            var schema = database.QuerySchema();

            schema["Entities1"]["SomeNullableInt"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableInt"].DefaultValue.ShouldBe(42);
            schema["Entities1"]["SomeString"].DefaultValue.ShouldBe("peter");
            schema["Entities1"]["SomeInt"].DefaultValue.ShouldBe(666);
            schema["Entities1"]["SomeDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).RequiresReprojection.ShouldBe(false);
        }

        [Fact]
        public void IsSafe()
        {
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Unsafe.ShouldBe(false);
        }
    }
}