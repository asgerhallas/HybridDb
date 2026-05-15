using System;
using System.Linq;
using System.Transactions;
using HybridDb.LiveQuery;
using HybridDb.Tests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.LiveQueryTests
{
    public class DocumentStore_LiveQueryTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void RecordsInsertedChanges()
        {
            UseRealTables();
            configuration.UseLiveQuery();

            Document<Entity>().With(x => x.Property);

            TouchStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;

            var firstId = NewId();
            store.Insert(table, firstId, new { Property = "first" });

            var results1 = store.QueryChanges(table, new byte[8]).ToList();

            var secondId = NewId();
            store.Insert(table, secondId, new { Property = "second" });

            var results2 = store.QueryChanges(table, results1[0].Position).ToList();

            results1.Count.ShouldBe(1);
            results1[0].DocumentId.ShouldBe(firstId);
            results1[0].Operation.ShouldBe(Operation.Inserted);

            results2.Count.ShouldBe(1);
            results2[0].DocumentId.ShouldBe(secondId);
            results2[0].Operation.ShouldBe(Operation.Inserted);
        }

        [Fact]
        public void RecordsUpdatedChanges()
        {
            UseRealTables();
            configuration.UseLiveQuery();

            Document<Entity>().With(x => x.Property);

            TouchStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var id = NewId();

            var etag = store.Insert(table, id, new { Property = "first" });
            var baseline = store.QueryChanges(table, new byte[8]).ToList();

            store.Update(table, id, etag, new { Property = "second" });

            var results = store.QueryChanges(table, baseline[0].Position).ToList();

            results.Count.ShouldBe(1);
            results[0].DocumentId.ShouldBe(id);
            results[0].Operation.ShouldBe(Operation.Updated);
        }

        [Fact]
        public void RecordsDeletedChanges()
        {
            UseRealTables();
            configuration.UseLiveQuery();

            Document<Entity>().With(x => x.Property);

            TouchStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var id = NewId();

            var etag = store.Insert(table, id, new { Property = "first" });
            var baseline = store.QueryChanges(table, new byte[8]).ToList();

            store.Delete(table, id, etag);

            var results = store.QueryChanges(table, baseline[0].Position).ToList();

            results.Count.ShouldBe(1);
            results[0].DocumentId.ShouldBe(id);
            results[0].Operation.ShouldBe(Operation.Deleted);
        }

        [Theory]
        [InlineData(TableMode.RealTables)]
        [InlineData(TableMode.GlobalTempTables)]
        public void UsesRowVersionOrderingForConcurrentUpdates(TableMode mode)
        {
            var snapshot = new TransactionOptions { IsolationLevel = IsolationLevel.Snapshot };
            var readCommitted = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted };

            Use(mode);
            UseTableNamePrefix(nameof(UsesRowVersionOrderingForConcurrentUpdates));
            configuration.UseLiveQuery();

            Document<Entity>().With(x => x.Property);

            TouchStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var id1 = NewId();
            var id2 = NewId();

            var etag1 = store.Insert(table, id1, new { Property = "first" });
            var etag2 = store.Insert(table, id2, new { Property = "second" });

            var baseline = store.QueryChanges(table, new byte[8]).ToList();
            var lastSeenPosition = baseline[1].Position;

            using (var tx1 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
            {
                store.Update(table, id1, etag1, new { Property = "first updated" });

                using (var tx2 = new TransactionScope(TransactionScopeOption.RequiresNew, readCommitted, TransactionScopeAsyncFlowOption.Enabled))
                {
                    store.Update(table, id2, etag2, new { Property = "second updated" });
                    tx2.Complete();
                }

                using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
                {
                    var results = store.QueryChanges(table, lastSeenPosition).ToList();

                    if (results.Any())
                    {
                        lastSeenPosition = results[0].Position;
                    }
                }

                tx1.Complete();
            }

            using (new TransactionScope(TransactionScopeOption.RequiresNew, snapshot, TransactionScopeAsyncFlowOption.Enabled))
            {
                var results = store.QueryChanges(table, lastSeenPosition).ToList();

                results.Count.ShouldBe(2);
                results.Select(x => x.DocumentId).ShouldBe([id1, id2], ignoreOrder: true);
                results.All(x => x.Operation == Operation.Updated).ShouldBeTrue();
            }
        }

        [Fact]
        public void ThrowsWhenConsumerFallsBehindRetentionWindow()
        {
            UseRealTables();
            configuration.UseLiveQuery();

            Document<Entity>().With(x => x.Property);

            TouchStore();

            var table = store.Configuration.GetDesignFor<Entity>().Table;
            var id1 = NewId();
            store.Insert(table, id1, new { Property = "first" });

            var stalePosition = store.QueryChanges(table, new byte[8]).Single().Position;
            var changeLogTable = store.Configuration.Tables.Values.OfType<ChangeLogTable>().Single();

            store.Database.RawExecute(
                $"update {store.Database.FormatTableNameAndEscape(changeLogTable.Name)} set CreatedAt = @CreatedAt",
                new { CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) });

            store.PruneChangesOlderThan(DateTimeOffset.UtcNow.AddDays(-1)).ShouldBe(1);

            var id2 = NewId();
            store.Insert(table, id2, new { Property = "second" });

            Should.Throw<LiveQueryPositionOutOfRangeException>(() => store.QueryChanges(table, stalePosition).ToList());
        }
    }
}
