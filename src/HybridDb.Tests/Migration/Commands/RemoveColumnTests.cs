using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RemoveColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesColumn(TableMode mode)
        {
            Use(mode);
            new CreateTable(new Table("Entities")).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RemoveColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);

            store.Schema.GetColumn("Entities", "SomeColumn").ShouldBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("OtherName", false)]
        public void CommandSafetyDependsOnColumnName(string columnName, bool isUnsafe)
        {
            new RemoveColumn("Entities", new Column(columnName, typeof(int))).Unsafe.ShouldBe(isUnsafe);
        }
    }
}