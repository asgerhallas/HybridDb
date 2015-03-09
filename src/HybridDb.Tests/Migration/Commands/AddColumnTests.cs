using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class AddColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))).Execute(store);

            new AddColumn("Entities", new Column("Col2", typeof (int))).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col2"].ShouldNotBe(null);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables, typeof(int))]
        [InlineData(TableMode.UseRealTables, typeof(int))]
        [InlineData(TableMode.UseTempTables, typeof(double))]
        [InlineData(TableMode.UseRealTables, typeof(double))]
        [InlineData(TableMode.UseTempTables, typeof(string))]
        [InlineData(TableMode.UseRealTables, typeof(string))]
        public void ColumnIsOfCorrectType(TableMode mode, Type type)
        {
            Use(TableMode.UseRealTables);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))).Execute(store);

            new AddColumn("Entities", new Column("Col2", type)).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col2"].Type.ShouldBe(type);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void SetsColumnAsNullableAndUsesUnderlyingTypeWhenNullable(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))).Execute(store);

            new AddColumn("Entities", new Column("Col2", typeof(int?))).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col2"].Type.ShouldBe(typeof(int));
            store.Schema.GetSchema()["Entities"]["Col2"].Nullable.ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CanSetColumnAsPrimaryKey(TableMode mode)
        {
            Use(mode);

            new CreateTable(new Table("Entities1", new Column("test", typeof(int)))).Execute(store);
            new AddColumn("Entities1", new Column("SomeInt", typeof(int), new SqlColumn(DbType.Int32, isPrimaryKey: true))).Execute(store);

            store.Schema.GetSchema()["Entities1"]["SomeInt"].IsPrimaryKey.ShouldBe(true);
        }

        [Fact]
        public void IsSafe()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).Unsafe.ShouldBe(false);
        }

        [Fact]
        public void RequiresReprojection()
        {
            new AddColumn("Entities", new Column("Col", typeof(int))).RequiresReprojection.ShouldBe(true);
        }
    }
}