using System.Linq;
using System.Transactions;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DocumentStore_QueryChangesTests : HybridDbAutoInitializeTests
    {
        [Fact]
        public void QueueInserts()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            store.Insert(table, NewId(), new {Property = "first"});
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Insert(table, NewId(), new {Property = "second"});
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public void QueueUpdates()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new {Property = "first"});
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Update(table, id, etag1, new {Property = "second"});
            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("second");
            results2[0].LastOperation.ShouldBe(Operation.Updated);
        }

        [Fact]
        public void QueueDeletes()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new {Property = "first"});
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();

            store.Delete(table, id, etag1);

            var results2 = store.Query<string>(table, results1[0].RowVersion, "Property").ToList();

            results2.Count.ShouldBe(1);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
        }

        [Fact]
        public void CanReinsertDeleted()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new {Property = "first"});
            store.Delete(table, id, etag1);
            store.Insert(table, id, new {Property = "second"});

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public void CanRedeleteReinserted()
        {
            UseRealTables();

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id = NewId();

            var etag1 = store.Insert(table, id, new {Property = "first"});
            store.Delete(table, id, etag1);
            var etag2 = store.Insert(table, id, new {Property = "second"});
            store.Delete(table, id, etag2);

            var results2 = store.Query<string>(table, new byte[8], "Property").ToList();

            results2.Count.ShouldBe(2);
            results2[0].Data.ShouldBe("first");
            results2[0].LastOperation.ShouldBe(Operation.Deleted);
            results2[1].Data.ShouldBe("second");
            results2[1].LastOperation.ShouldBe(Operation.Deleted);
        }

        [Fact]
        public void Bug_RaceConditionWithSnapshotAndRowVersion()
        {
            //Nummeret til rowversion kolonnen tildeles ved starten af tx, hvilket betyder at ovenstående giver følgende situation:

            //1. Tx1 starter og row A opdateres.Får tildelt rowversion = 1.
            //2. Tx2 starter og row B opdateres. Får tildelt rowversion = 2 og comittes.
            //3. Tx3 starter og læser højeste nuværende rowversion, som er 2 og kører sin opdatering og gemmer sidst læste version som 2. 
            //4. Tx1 comittes og har stadig rowversion 1, men næste gang vi forespørger efter opdateringer, så kigger vi kun på rowversions højere end 2.
            //   Derfor misser vi Tx1 opdateringen.

            //Se https://stackoverflow.com/questions/28444599/implementing-incremental-client-updates-with-rowversions-in-postgres for detaljer.

            //Løsningen er at bruge `min-active-rowversion` til sætte et øvre grænse for hvilken version, der må læses.Så i ovenstående tilfælde vil `min-active-rowversion` være 1
            //og derfor vil vi kun læse op til rowversion 1, hvilket vil sige at opdatering 2 først kan blive læst, når 1 er comitted.

            var snapshot = new TransactionOptions {IsolationLevel = IsolationLevel.Snapshot};
            var readCommitted = new TransactionOptions {IsolationLevel = IsolationLevel.ReadCommitted};

            UseRealTables();
            UseTableNamePrefix(nameof(Bug_RaceConditionWithSnapshotAndRowVersion));

            Document<Entity>().With(x => x.Property);

            InitializeStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var id1 = NewId();
            var id2 = NewId();

            // the race condition only happens on updates, because inserts locks the primary key index and thus is not concurrent
            var etag1 = store.Insert(table, id1, new {Property = "first"});
            var etag2 = store.Insert(table, id2, new {Property = "second"});

            // get the initial row version after insert
            var results1 = store.Query<string>(table, new byte[8], "Property").ToList();
            var lastSeenRowVersion = results1[0].RowVersion;

            using (var tx1 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
            {
                store.Update(table, id1, etag1, new {Property = "first updated"});

                using (var tx2 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
                {
                    store.Update(table, id2, etag2, new {Property = "second updated"});

                    tx2.Complete();
                }

                using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
                {
                    // get latest completed updates
                    var results2 = store.Query<string>(table, lastSeenRowVersion, "Property").ToList();

                    // the query should not return anything when the race condition is fixed, but before fixing
                    // it will return the "second update" and lastSeenRowVersion will be set to a too high number
                    // so "first update" will never be seen.
                    if (results2.Any())
                    {
                        lastSeenRowVersion = results2[0].RowVersion;
                    }
                }

                tx1.Complete();
            }

            using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
            {
                // now that both updates are fully complete, expect to see them both - nothing skipped.
                var results3 = store.Query<string>(table, lastSeenRowVersion, "Property").ToList();

                results3.Count.ShouldBe(2);
                results3[0].Data.ShouldBe("first updated");
                results3[1].Data.ShouldBe("second updated");
            }
        }
    }
}