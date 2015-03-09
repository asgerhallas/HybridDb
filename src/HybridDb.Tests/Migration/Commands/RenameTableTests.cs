using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RenameTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesTable(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities", new Column("col1", typeof(int)))).Execute(store);

            new RenameTable("Entities", "OtherEntities").Execute(store);

            store.Schema.GetSchema().ShouldNotContainKey("Entities");
            store.Schema.GetSchema().ShouldContainKey("OtherEntities");
        }


        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RenameTable("Entities", "OtherEntities").RequiresReprojection.ShouldBe(false);
        }

        [Fact]
        public void IsSafe()
        {
            new RenameTable("Entities", "OtherEntities").Unsafe.ShouldBe(false);
        }
    }
}