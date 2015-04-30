using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveColumnTests : HybridDbStoreTests
    {
        [Fact]
        public void RemovesColumn()
        {
            Use(TableMode.UseRealTables);

            var table = new Table("Entities", new Column("FirstColumn", typeof (int)));
            new CreateTable(table).Execute(database);
            new AddColumn("Entities", new Column("SomeColumn", typeof(int))).Execute(database);

            new RemoveColumn(table, "SomeColumn").Execute(database);

            database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory(Skip = "Bug in SQL server 2012 prevents us from removing temp tables")]
        [InlineData(TableMode.UseGlobalTempTables)]
        [InlineData(TableMode.UseTempTables)]
        public void RemovesTempTableColumn(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities", new Column("FirstColumn", typeof(int)));
            new CreateTable(table).Execute(database);
            new AddColumn("Entities", new Column("SomeColumn", typeof(int))).Execute(database);

            new RemoveColumn(table, "SomeColumn").Execute(database);

            database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("Id", true)]
        [InlineData("OtherName", false)]
        public void IsSafeDependingOnColumnName(string columnName, bool isUnsafe)
        {
            new RemoveColumn(new Table("Entities"), columnName).Unsafe.ShouldBe(isUnsafe);
        }

        [Fact]
        public void DoesNotRequireReProjection()
        {
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojectionOf.ShouldBe(null);
        }
    }
}