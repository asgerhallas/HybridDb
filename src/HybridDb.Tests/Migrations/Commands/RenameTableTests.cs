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
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void RenamesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            new CreateTable(new Table("Entities", new Column("col1", typeof(int)))).Execute(documentStore.Database);

            new RenameTable("Entities", "OtherEntities").Execute(documentStore.Database);

            documentStore.Database.QuerySchema().ShouldNotContainKey("Entities");
            documentStore.Database.QuerySchema().ShouldContainKey("OtherEntities");
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