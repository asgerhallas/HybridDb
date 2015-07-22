using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RenameColumnTests : HybridDbStoreTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesColumn(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities", new Column("Col1", typeof(int)));

            new CreateTable(table).Execute(database);
            new AddColumn("Entities", new Column("SomeColumn", typeof(int))).Execute(database);

            new RenameColumn(table, "SomeColumn", "SomeNewColumn").Execute(database);

            database.QuerySchema()["Entities"]["SomeColumn"].ShouldBe(null);
            database.QuerySchema()["Entities"]["SomeNewColumn"].ShouldNotBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("OtherName", false)]
        public void CommandSafetyDependsOnColumnName(string columnName, bool isUnsafe)
        {
            new RenameColumn(new Table("Entities"), columnName, "SomeColumn").Unsafe.ShouldBe(isUnsafe);
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RenameColumn(new Table("Entities"), "c1", "c2").RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsSafe()
        {
            new RenameColumn(new Table("Entities"), "c1", "c2").Unsafe.ShouldBe(false);
        }
    }
}