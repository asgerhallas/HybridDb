using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RenameColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void RenamesColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            var table = new Table("Entities", new Column("Col1", typeof(int)));

            store.Execute(new CreateTable(table));
            store.Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            store.Execute(new RenameColumn(table, "SomeColumn", "SomeNewColumn"));

            store.Database.QuerySchema()["Entities"].ShouldNotContain("SomeColumn");
            store.Database.QuerySchema()["Entities"].ShouldContain("SomeNewColumn");
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
            new RenameColumn(new Table("Entities"), "c1", "c2").RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsSafe()
        {
            new RenameColumn(new Table("Entities"), "c1", "c2").Unsafe.ShouldBe(false);
        }
    }
}