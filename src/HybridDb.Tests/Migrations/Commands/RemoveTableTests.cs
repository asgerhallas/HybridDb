using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveTableTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseLocalTempTables)]
        [InlineData(TableMode.UseGlobalTempTables)]
        [InlineData(TableMode.UseRealTables)]
        public void RemovesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))).Execute(store.Database);

            new RemoveTable("Entities").Execute(store.Database);

            store.Database.QuerySchema().ShouldNotContainKey("Entities");
        }

        [Fact]
        public void DoesNotRequireReprojection()
        {
            new RemoveTable("Entities").RequiresReprojectionOf.ShouldBe(null);
        }

        [Fact]
        public void IsUnsafe()
        {
            new RemoveTable("Entities").Unsafe.ShouldBe(true);
        }
    }
}