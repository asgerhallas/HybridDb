using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RenameTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            new CreateTable(new Table("Entities", new Column("col1", typeof(int)))).Execute(store.Database);

            new RenameTable("Entities", "OtherEntities").Execute(store.Database);

            store.Database.QuerySchema().ShouldNotContainKey("Entities");
            store.Database.QuerySchema().ShouldContainKey("OtherEntities");
        }


        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RenameTable("Entities", "OtherEntities").RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsSafe()
        {
            new RenameTable("Entities", "OtherEntities").Unsafe.ShouldBe(false);
        }
    }
}