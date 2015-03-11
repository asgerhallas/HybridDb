using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RemoveTableTests : HybridDbStoreTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesTable(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(database);

            new RemoveTable("Entities").Execute(database);

            store.Database.QuerySchema().ShouldNotContainKey("Entities");
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RemoveTable("Entities").RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsUnsafe()
        {
            new RemoveTable("Entities").Unsafe.ShouldBe(true);
        }
    }
}