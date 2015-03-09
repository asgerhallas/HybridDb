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
            //new CreateTable(new Table("Entities", new Column("Col1", typeof(string)) { IsPrimaryKey = true })).Execute(store);

            store.Schema.GetSchema()["Entities"]["Col1"].IsPrimaryKey.ShouldBe(true);
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