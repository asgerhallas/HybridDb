using System.Data;
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

            new CreateTable(new Table("Entities")).Execute(store.Database);
            
            store.Database.TableExists("Entities").ShouldBe(true);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void CreatesColumns(TableMode mode)
        {
            Use(mode);

            var table = new Table("Entities");
            table.Register(new Column("SomeColumn", typeof(int)));

            new CreateTable(table).Execute(store.Database);
            
            store.Database.TableExists("Entities").ShouldBe(true);
            store.Database.GetType(store.Database.GetColumn("Entities", "SomeColumn").system_type_id).ShouldBe("int");
        }
    }
}