using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RemoveColumnTests : HybridDbStoreTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesColumn(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities", new Column("FirstColumn", typeof (int)));
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
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojection.ShouldBe(false);
        }
    }
}