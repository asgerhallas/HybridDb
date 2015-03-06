using System;
using HybridDb.Config;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration.Commands
{
    public class RenameTableTests : HybridDbTests
    {
        [Fact]
        public void RenamesTable()
        {
            Use(TableMode.UseRealTables);
            new CreateTable(new Table("Entities")).Execute(store);

            new RenameTable("Entities", "OtherEntities").Execute(store);

            store.Schema.GetSchema().ShouldNotContainKey("Entities");
            store.Schema.GetSchema().ShouldNotContainKey("OtherEntities");
        }

        [Fact]
        public void ThrowsWhenTryingToRenameTempTables()
        {
            Use(TableMode.UseTempTables);
            new CreateTable(new Table("Entities")).Execute(store);

            Should.Throw<NotSupportedException>(() => new RenameTable("Entities", "OtherEntities").Execute(store));            
        }
    }
}