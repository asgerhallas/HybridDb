using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class CreateTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesTable(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(store);

            store.Schema.GetSchema().ShouldContainKey("Entities");
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesColumns(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(store);

            store.Schema.GetSchema().ShouldContainKey("Entities");
            store.Schema.GetSchema()["Entities"]["Col1"].Type.ShouldBe(typeof(string));
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesPrimaryKeyColumn(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities", new Column("Col1", typeof(string), new SqlColumn(DbType.String, isPrimaryKey: true)))).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col1"].IsPrimaryKey.ShouldBe(true);
        }

        //[Fact]
        //public void WillQuoteTableAndColumnNamesOnCreation()
        //{
        //    Should.NotThrow(() => storeWithRealTables.Migrate(
        //        migrator => migrator.AddTable("Create", "By int")));
        //}

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        //[InlineData(TableMode.UseRealTables)]
        public void CanCreateColumnWithDefaultValue(TableMode mode)
        {
            Use(mode);

            //var c = new Column("SomeOtherNullableInt", typeof(int), new SqlColumn(DbType.Int32, nullable: true, defaultValue: 42));
            //var createTable = new CreateTable(new Table("Entities1", c));
            //createTable.Execute(store);

            new CreateTable(new Table("Entities1",
                new Column("SomeNullableInt", typeof(int), new SqlColumn(DbType.Int32, nullable: true, defaultValue: null)),
                new Column("SomeOtherNullableInt", typeof(int), new SqlColumn(DbType.Int32, nullable: true, defaultValue: 42)),
                new Column("SomeString", typeof(string), new SqlColumn(DbType.String, defaultValue: "peter")),
                new Column("SomeInt", typeof(int), new SqlColumn(DbType.Int32, defaultValue: 666)),
                new Column("SomeDateTime", typeof(DateTime), new SqlColumn(DbType.DateTime2, defaultValue: new DateTime(1999, 12, 24))))).Execute(store);

            var schema = store.Schema.GetSchema();

            //schema["Entities1"]["SomeNullableInt"].DefaultValue.ShouldBe(null);
            schema["Entities1"]["SomeOtherNullableInt"].DefaultValue.ShouldBe(42);
            //schema["Entities1"]["SomeString"].DefaultValue.ShouldBe("peter");
            //schema["Entities1"]["SomeInt"].DefaultValue.ShouldBe(666);
            //schema["Entities1"]["SomeDateTime"].DefaultValue.ShouldBe(new DateTime(1999, 12, 24));
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