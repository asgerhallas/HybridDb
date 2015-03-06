using HybridDb.Config;
using HybridDb.Migration.Commands;
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

            new CreateTable(new Table("Entities")).Execute(store);
            
            store.Schema.TableExists("Entities").ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesColumns(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities");
            table.Register(new Column("SomeColumn", typeof(int)));

            new CreateTable(table).Execute(store);
            
            store.Schema.TableExists("Entities").ShouldBe(true);
            store.Schema.GetColumn("Entities", "SomeColumn").Type.ShouldBe(typeof(int));
        }
    }
}