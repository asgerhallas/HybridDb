using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RemoveTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesTable(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities")).Execute(store);

            new RemoveTable("Entities").Execute(store);

            store.Schema.GetSchema().ShouldNotContainKey("Entities");
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RemoveTable("Entities").RequiresReprojection.ShouldBe(false);
        }

        [Fact]
        public void IsUnsafe()
        {
            new RemoveTable("Entities").Unsafe.ShouldBe(true);
        }
    }
}