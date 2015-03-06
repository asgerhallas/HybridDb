using System;
using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migration.Commands
{
    public class RenameColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesColumn(TableMode mode)
        {
            Use(TableMode.UseRealTables);
            new CreateTable(new Table("Entities")).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RenameColumn("Entities", "SomeColumn", "SomeNewColumn").Execute(store);

            store.Schema.GetColumn("Entities", "SomeColumn").ShouldBe(null);
            store.Schema.GetColumn("Entities", "SomeNewColumn").ShouldNotBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("OtherName", false)]
        public void CommandSafetyDependsOnColumnName(string columnName, bool isUnsafe)
        {
            new RenameColumn("Entities", columnName, "SomeColumn").Unsafe.ShouldBe(isUnsafe);
        }
    }
}