using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RenameColumnTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseLocalTempTables)]
        [InlineData(TableMode.UseGlobalTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());

            var table = new Table("Entities", new Column("Col1", typeof(int)));

            new CreateTable(table).Execute(store.Database);
            new AddColumn("Entities", new Column("SomeColumn", typeof(int))).Execute(store.Database);

            new RenameColumn(table, "SomeColumn", "SomeNewColumn").Execute(store.Database);

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