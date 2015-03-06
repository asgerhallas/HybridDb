using System;
using HybridDb.Configuration;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

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

            store.Schema.TableExists("Entities").ShouldBe(false);
            store.Schema.TableExists("OtherEntities").ShouldBe(true);
        }

        [Fact]
        public void ThrowsWhenTryingToRenameTempTables()
        {
            Use(TableMode.UseTempTables);
            new CreateTable(new Table("Entities")).Execute(store);

            Should.Throw<InvalidOperationException>(() => new CreateTable(new Table("Entities")).Execute(store));            
            store.Schema.TableExists("Entities").ShouldBe(true);
        }
    }
}