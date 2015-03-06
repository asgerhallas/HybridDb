using System;
using System.Linq;
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
        [InlineData(typeof(int))]
        [InlineData(typeof(int?))]
        [InlineData(typeof(string))]
        public void ColumnIsOfCorrectType(Type type)
        {
            Use(TableMode.UseRealTables);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))).Execute(store);

            new AddColumn("Entities", new Column("Col2", type)).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col2"].Type.ShouldBe(typeof(int));
        }


        [Fact]
        public void CanSetColumnAsPrimaryKey(Type type)
        {
            throw new NotImplementedException();

            //Use(TableMode.UseRealTables);
            //new CreateTable(new Table("Entities")).Execute(store);

            //new AddColumn("Entities", new Column("SomeColumn", type, isPrimaryKey: true)).Execute(store);

            //store.Schema.GetColumn("Entities", "SomeColumn").IsPrimaryKey.ShouldBe(true);
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