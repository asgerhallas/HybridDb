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

            var table = new Table("Entities");
            new CreateTable(table).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RemoveColumn(table, "SomeColumn").Execute(store);

            store.Schema.GetSchema()["Entities"]["SomeColumn"].ShouldBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("OtherName", false)]
        public void CommandSafetyDependsOnColumnName(string columnName, bool isUnsafe)
        {
            var table = new Table("Entities");
            new RemoveColumn(table, columnName).Unsafe.ShouldBe(isUnsafe);
        }
    }
}