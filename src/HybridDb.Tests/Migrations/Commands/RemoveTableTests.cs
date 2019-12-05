using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class RemoveTableTests : HybridDbTests
    {
        public RemoveTableTests() => NoInitialize();

        [Theory]
        [InlineData(TableMode.GlobalTempTables)]
        [InlineData(TableMode.RealTables)]
        public void RemovesTable(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            store.Execute(new CreateTable(new Table("Entities", new Column("Col1", typeof(string)))));

            store.Execute(new RemoveTable("Entities"));

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
            new RemoveTable("Entities").Safe.ShouldBe(false);
        }
    }
}