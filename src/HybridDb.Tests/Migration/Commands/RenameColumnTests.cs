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
        [Fact]
        public void AddsColumn()
        {
            Use(TableMode.UseRealTables);
            new CreateTable(new Table("Entities")).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RenameColumn("Entities", "SomeColumn", "SomeNewColumn").Execute(store);

            store.Schema.GetColumn("Entities", "SomeColumn").ShouldBe(null);
            store.Schema.GetColumn("Entities", "SomeNewColumn").ShouldNotBe(null);
        }

        [Fact]
        public void ThrowsWhenNotRealTables()
        {
            Use(TableMode.UseTempTables);

            Should.Throw<NotSupportedException>(() => new RenameColumn("Entities", "SomeColumn", "SomeNewColumn").Execute(store));
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