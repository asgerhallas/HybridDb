using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Threading.Tasks;
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

            var commits = ReadEventsFrom(store, 0);

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

        [Fact]
        public void ReadEvents()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0, "test1")),
                CreateAppendEventCommand(CreateEventData("stream-1", 1, "test2")));

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "test123")));

            var commits = ReadEventsFrom(store, 0);
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
            Should.NotThrow(() => ReadEventsFrom(store, 0));

            // this is actually the most common scenario for this error
            // when projectors try to catch up but there are no new commits
            Should.NotThrow(() => ReadEventsFrom(store, 100));
        }

        [Fact]
        public void ReadEventsConcurrently()
        {
            ExecuteManyAppendEventCommands(store, "stream-1", 0, 1000);

            Parallel.For(0, 100, i => ReadEventsFrom(store, 0));
        }

        [Fact]
        public void ReadEventsFromPosition()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)),
                CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, "myspecialevent")));

            var domainEvents = ReadEventsFrom(store, 2);

            domainEvents.Single().Events.Single().Data.ShouldBe(new [] {(byte) 64});
        }

        [Fact]
        public void ReadWhileSaving()
        {
            ExecuteManyAppendEventCommands(store, "stream-1", 0, 2);

            var enumerator = ReadEventsFrom(store, 0).SelectMany(x => x.Events).GetEnumerator();

            enumerator.MoveNext();

            store.Execute(Enumerable.Range(2, 4).Select(i => CreateAppendEventCommand(CreateEventData("stream-1", i))));

            enumerator.MoveNext().ShouldBe(true);
            enumerator.MoveNext().ShouldBe(false);
            enumerator.Dispose();
        }

        static void ExecuteManyAppendEventCommands(DocumentStore store, string streamId, long start, int count) => 
            store.Execute(Enumerable.Range((int)start, count).Select(i => CreateAppendEventCommand(CreateEventData(streamId, i))));

        static List<Commit<byte[]>> ReadEventsFrom(DocumentStore store, long fromPositionIncluding) => 
            store.Transactionally(IsolationLevel.Snapshot, tx => tx.Execute(new ReadEvents(new EventTable("events"), fromPositionIncluding)).ToList());
    }
}