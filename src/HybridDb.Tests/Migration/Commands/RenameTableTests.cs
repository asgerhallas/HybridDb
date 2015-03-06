using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
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
            new CreateTable(new Table("Entities")).Execute(store);

            new RenameTable("Entities", "OtherEntities").Execute(store);

            store.Schema.GetSchema().ShouldNotContainKey("Entities");
            store.Schema.GetSchema().ShouldNotContainKey("OtherEntities");
        }
    }
}