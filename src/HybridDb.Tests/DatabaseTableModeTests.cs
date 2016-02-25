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
                var configurator = new LambdaHybridDbConfigurator(x => x.Document<Case>());

                using (DocumentStore.ForTesting(TableMode.UseTempDb, connectionString, configurator: configurator))
                using (DocumentStore.ForTesting(TableMode.UseTempTables, connectionString, configurator: configurator)) { }
            });
        }
    }
}