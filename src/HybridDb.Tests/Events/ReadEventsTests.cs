using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb.Tests.Events
{
    public class ReadEventsTests : EventStoreTests
    {
        public ReadEventsTests(ITestOutputHelper output) : base(output)
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

            var commits = Execute(new ReadEvents(new EventTable("events"), 0, false));

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
                    tx3.Execute(new ReadEvents(new EventTable("events"), 0, false)).ShouldBeEmpty();
                });
            });

            store.Transactionally(IsolationLevel.Snapshot, tx4 =>
            {
                // now that both updates are fully complete, expect to see them both - nothing skipped.
                tx4.Execute(new ReadEvents(new EventTable("events"), 0, false)).Count().ShouldBe(2);
            });
        }

        [Fact]
        public void ReadEvents()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0, "test1")),
                CreateAppendEventCommand(CreateEventData("stream-1", 1, "test2")));

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "test123")));

            var commits = Execute(new ReadEvents(new EventTable("events"), 0, false));

            var stream1 = commits[0].Events;
            var stream2 = commits[1].Events;

            commits.Count.ShouldBe(2);
            stream1.Count.ShouldBe(2);
            stream2.Count.ShouldBe(1);

            stream1[0].Name.ShouldBe("test1");
            stream1[1].Name.ShouldBe("test2");
            stream2[0].Name.ShouldBe("test123");
        }

        [Fact]
        public void DoesNotFailOnEmptyStream()
        {
            Should.NotThrow(() => Execute(new ReadEvents(new EventTable("events"), 0, false)));

            // this is actually the most common scenario for this error
            // when projectors try to catch up but there are no new commits
            Should.NotThrow(() => Execute(new ReadEvents(new EventTable("events"), 100, false)));
        }

        [Fact]
        public void ReadEventsConcurrently()
        {
            store.Execute(Enumerable.Range(0, 1000).Select(i => CreateAppendEventCommand(CreateEventData("stream-1", i))));

            Parallel.For(0, 100, i => Execute(new ReadEvents(new EventTable("events"), 0, false)));
        }

        [Fact]
        public void ReadEventsFromPosition()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)));

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "myspecialevent")));

            var domainEvents = Execute(new ReadEvents(new EventTable("events"), 2, false));

            domainEvents.Single().Events.Single().Name.ShouldBe("myspecialevent");
        }

        [Fact]
        public void ReadWhileSaving()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)));

            store.Transactionally(IsolationLevel.Snapshot, tx =>
            {
                using (var enumerator = tx.Execute(new ReadEvents(new EventTable("events"), 0, false)).SelectMany(x => x.Events).GetEnumerator())
                {
                    enumerator.MoveNext().ShouldBe(true);

                    store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2)));

                    enumerator.MoveNext().ShouldBe(true);
                    enumerator.MoveNext().ShouldBe(false);
                }
            });
        }

        List<Commit<byte[]>> Execute(ReadEvents command) => 
            store.Transactionally(IsolationLevel.Snapshot, tx => tx.Execute(command).ToList());
    }
}