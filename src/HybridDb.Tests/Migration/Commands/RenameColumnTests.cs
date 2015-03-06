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
            Use(mode);

            var table = new Table("Entities", new Column("Col1", typeof(int)));
            
            new CreateTable(table).Execute(store);
            new AddColumn("Entities", new Column("SomeColumn", typeof (int))).Execute(store);
            
            new RenameColumn(table, "SomeColumn", "SomeNewColumn").Execute(store);

            store.Schema.GetSchema()["Entities"]["SomeColumn"].ShouldBe(null);
            store.Schema.GetSchema()["Entities"]["SomeNewColumn"].ShouldNotBe(null);
        }

        [Theory]
        [InlineData("Document", true)]
        [InlineData("OtherName", false)]
        public void CommandSafetyDependsOnColumnName(string columnName, bool isUnsafe)
        {
            new RenameColumn(new Table("Entities"), columnName, "SomeColumn").Unsafe.ShouldBe(isUnsafe);
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RenameColumn(new Table("Entities"), "c1", "c2").RequiresReprojection.ShouldBe(false);
        }

        [Fact]
        public void IsSafe()
        {
            new RenameColumn(new Table("Entities"), "c1", "c2").Unsafe.ShouldBe(false);
        }
    }
}