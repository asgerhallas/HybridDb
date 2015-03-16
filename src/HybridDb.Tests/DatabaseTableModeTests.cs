using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DatabaseTableModeTests
    {
        [Fact]
        public void CanUseTempAndGlobalTempTablesConcurrently()
        {
            Should.NotThrow(() =>
            {
                var configurator = new LambdaHybridDbConfigurator(x => x.Document<Case>());

                using (DocumentStore.ForTesting(TableMode.UseGlobalTempTables, configurator: configurator))
                using (DocumentStore.ForTesting(TableMode.UseTempTables, configurator: configurator)) { }
            });
        }
    }
}