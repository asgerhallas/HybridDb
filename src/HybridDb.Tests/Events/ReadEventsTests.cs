using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb.Tests.Events
{
    public class ReadEventsTests : EventStoreTests
    {
        public ReadEventsTests()
        {
            UseEventStore();
        }

        [Fact]
        public void OrderedByPosition()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0, "a")));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1, "b")));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "c")));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2, "d")));

            var commits = store.Transactionally(IsolationLevel.Snapshot, tx => tx.Execute(new ReadEvents(new EventTable("events"), 0)).ToList());

            commits
                .SelectMany(x => x.Events.Select(e => e.Name))
                .ShouldBe(new[] {"a", "b", "c", "d"});
        }

        [Fact]
        public void NoReadPast_MinActiveTransaction()
        {
            UseRealTables();
            UseTableNamePrefix(nameof(NoReadPast_MinActiveTransaction));

            InitializeStore();

            store.Transactionally(IsolationLevel.ReadCommitted, tx1 =>
            {
                tx1.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0, "a")));

                store.Transactionally(IsolationLevel.ReadCommitted, tx2 =>
                {
                    tx2.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "b")));
                });

                store.Transactionally(IsolationLevel.Snapshot, tx3 =>
                {
                    // get latest completed updates
                    // the query should not return anything when the race condition is fixed
                    tx3.Execute(new ReadEvents(new EventTable("events"), 0)).ShouldBeEmpty();
                });
            });

            store.Transactionally(IsolationLevel.Snapshot, tx4 =>
            {
                // now that both updates are fully complete, expect to see them both - nothing skipped.
                tx4.Execute(new ReadEvents(new EventTable("events"), 0)).Count().ShouldBe(2);
            });
        }
    }
}