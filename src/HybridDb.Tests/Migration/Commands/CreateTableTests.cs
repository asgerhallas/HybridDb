using HybridDb.Migration.Commands;
using HybridDb.Schema;
using Shouldly;
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

            var command = new CreateTable(new Table("Entities"));

            command.Execute(store);
            
            store.TableExists("Entities").ShouldBe(true);
        }
    }
}