using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RenameTableTests : HybridDbTests
    {
        public RenameTableTests(ITestOutputHelper output) : base(output) => NoInitialize();

        [Theory]
        [InlineData(TableMode.RealTables)]
        public void RenamesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            store.Execute(new CreateTable(new Table("Entities", new Column("col1", typeof(int)))));

            store.Execute(new RenameTable("Entities", "OtherEntities"));

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
            new RenameTable("Entities", "OtherEntities").Safe.ShouldBe(true);
        }
    }
}