using System;
using System.Data;
using System.Linq;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class ReadStreamTests : EventStoreTests
    {
        public ReadStreamTests()
        {
            UseEventStore();
        }

        [Fact]
        public void LoadWithCutOffOnPosition()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0, 1)).ToList(), IsolationLevel.Snapshot);

            events.Select(x => x.SequenceNumber).ShouldBe(new long[] {0, 1});
        }

        [Fact]
        public void LoadWithCutOffOnPosition_OnDifferentStream()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-0", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0, 2)).ToList(), IsolationLevel.Snapshot);

            events.Select(x => x.SequenceNumber).ShouldBe(new long[] {0, 1});
        }

        [Fact]
        public void DoesNotStreamEntireCommitAnyMore()
        {
            // We've changed the way this works. Now the client of store.Load is responsible 
            // for supplying a position that is not mid-commit - if that is what is needed
            // This is for performance-reasons mostly - and because the client usually knows the correct position

            store.Execute(Enumerable.Range(0, 100).Select(i => CreateAppendEventCommand(CreateEventData("stream-0", i))));

            var count = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-0", 0, 9)).Count(), IsolationLevel.Snapshot);

            count.ShouldBe(10);
        }
    }
}