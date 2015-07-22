using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveTableTests : HybridDbStoreTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesTable(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(database);

            new RemoveTable("Entities").Execute(database);
            
            database.QuerySchema().ShouldNotContainKey("Entities");
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