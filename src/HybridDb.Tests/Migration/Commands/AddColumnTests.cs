using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class AddColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities")).Execute(store);

            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);

            store.Schema.GetColumn("Entities", "SomeColumn").ShouldNotBe(null);
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void ColumnIsOfCorrectType(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities")).Execute(store);

            new AddColumn("Entities", new Column("SomeColumn", typeof(int))).Execute(store);

            store.Schema.GetType(store.Schema.GetColumn("Entities", "SomeColumn").system_type_id).ShouldBe("int");
        }
    }
}