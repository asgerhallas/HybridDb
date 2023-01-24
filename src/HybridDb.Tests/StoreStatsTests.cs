using System;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class StoreStatsTests : HybridDbTests
    {
        public StoreStatsTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void CheckLeaksException()
        {
            configuration.DisableBackgroundMigrations();

            using var tx = store.BeginTransaction();

            Should.Throw<Exception>(store.Stats.CheckLeaks,
@"Possible connection/transaction leaking detected:

Instance: connection
File Path: C:\Git\HybridDb\src\HybridDb\SqlServerUsingGlobalTempTables.cs
Line Number: 86
Member Name: Connect

Instance: transaction
File Path: C:\Git\HybridDb\src\HybridDb\DocumentTransaction.cs
Line Number: 30
Member Name: .ctor");
        }

        [Fact]
        public void GlobalTempTables()
        {
            configuration.DisableBackgroundMigrations();

            // It is intetional that we do not use the store provided by the base class.
            var documentStore = new DocumentStore(TableMode.GlobalTempTables, configuration, true);

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(1);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);

            var documentTransaction = documentStore.BeginTransaction();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(2);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(1);

            documentTransaction.Dispose();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(1);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);

            documentStore.Dispose();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(0);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);
        }

        [Fact]
        public void RealTables()
        {
            UseRealTables();

            configuration.DisableBackgroundMigrations();

            // It is intetional that we do not use the store provided by the base class.
            var documentStore = new DocumentStore(TableMode.RealTables, configuration, true);

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(0);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);

            var documentTransaction = documentStore.BeginTransaction();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(1);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(1);

            documentTransaction.Dispose();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(0);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);

            documentStore.Dispose();

            documentStore.Stats.NumberOfNumberUndisposedConnections.ShouldBe(0);
            documentStore.Stats.NumberOfUndisposedTransactions.ShouldBe(0);
        }
    }
}