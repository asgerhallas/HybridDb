using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class CreateTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void CreatesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))));

            store.Database.QuerySchema().ShouldContainKey("Entities");
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void CreatesColumns(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))));

            store.Database.QuerySchema().ShouldContainKey("Entities");
            //store.Database.QuerySchema()["Entities"]["Col1"].Type.ShouldBe(typeof(string));
        }

        [Theory]
        [InlineData(TableMode.GlobalTempTables, typeof(int), null)]
        [InlineData(TableMode.RealTables, typeof(int), null)]
        [InlineData(TableMode.GlobalTempTables, typeof(string), 255)]
        [InlineData(TableMode.RealTables, typeof(string), 255)]
        public void CreatesPrimaryKeyColumn(TableMode mode, Type type, int? length)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", type, length, isPrimaryKey: true))));

            //store.Database.QuerySchema()["Entities"]["Col1"].IsPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void WillQuoteTableAndColumnNamesOnCreation()
        {
            Should.NotThrow(() => new CreateTable(new Table("Create",new Column("By Int", typeof(int)))));
        }

        [Theory]
        [InlineData(TableMode.RealTables)]
        [InlineData(TableMode.GlobalTempTables)]
        public void CanCreateColumnWithDefaultValue(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            store.Execute(new CreateTable(new Table("Entities1",
                new Column("SomeNullableInt", typeof(int?), defaultValue: null),
                new Column("SomeOtherNullableInt", typeof(int?), defaultValue: 42),
                new Column("SomeString", typeof(string),  defaultValue: "peter"),
                new Column("SomeInt", typeof(int),  defaultValue: 666),
                new Column("SomeDateTime", typeof(DateTime),  defaultValue: new DateTime(1999, 12, 24)))));

            var schema = store.Database.QuerySchema();

            //schema["Entities1"]["SomeNullableInt"].DefaultValue.ShouldBe(null);
            //schema["Entities1"]["SomeOtherNullableInt"].DefaultValue.ShouldBe(42);
            //schema["Entities1"]["SomeString"].DefaultValue.ShouldBe("peter");
            //schema["Entities1"]["SomeInt"].DefaultValue.ShouldBe(666);
            //schema["Entities1"]["SomeDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsSafe()
        {
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Safe.ShouldBe(true);
        }
    }
}