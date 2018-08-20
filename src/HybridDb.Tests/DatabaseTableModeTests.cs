using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DatabaseTableModeTests : HybridDbTests
    {
        [Fact]
        public void CanUseTempAndGlobalTempTablesConcurrently()
        {
            Should.NotThrow(() =>
            {
                using (var store1 = DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, x => x.Document<Case>()))
                using (var store2 = DocumentStore.ForTesting(TableMode.UseTempTables, connectionString, x => x.Document<Case>()))
                {
                    store1.Configuration.UseTableNamePrefix("something");
                    store1.Initialize();
                    store2.Initialize();
                }
            });
        }
    }
}