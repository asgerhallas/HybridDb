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
        [Fact]
        public void RenamesColumn()
        {
            var table = new Table("Entities", new Column("Col1", typeof(int)));

            Execute(new CreateTable(table));
            Execute(new AddColumn("Entities", new Column("SomeColumn", typeof(int))));

            Execute(new RenameColumn(table, "SomeColumn", "SomeNewColumn"));

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