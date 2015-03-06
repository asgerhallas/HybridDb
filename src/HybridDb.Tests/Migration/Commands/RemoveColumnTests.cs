using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RemoveColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesColumn(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities");
            new CreateTable(table).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RemoveColumn(table, "SomeColumn").Execute(store);

            store.Schema.GetSchema()["Entities"]["SomeColumn"].ShouldBe(null);
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
        public void DoesNotRequireReprojection()
        {
            new RemoveColumn(new Table("Entities"), "Col").RequiresReprojection.ShouldBe(false);
        }
    }
}