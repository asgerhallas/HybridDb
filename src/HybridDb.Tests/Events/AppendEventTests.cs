using System;
using System.Data;
using System.Linq;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class AppendEventTests : EventStoreTests
    {
        public AppendEventTests()
        {
            UseEventStore();
        }

        [Fact]
        public void AppendEvent()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));

            var @event = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0)).Single(), IsolationLevel.Snapshot);

            @event.StreamId.ShouldBe("stream-1");
            @event.SequenceNumber.ShouldBe(0);
            @event.Name.ShouldBe("myevent");
            @event.Data.ShouldBe(new byte[] {0});
        }

        [Fact]
        public void FailsOnDuplicateEventId()
        {
            var eventId = Guid.NewGuid();

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0, eventId)));

            Should.Throw<ConcurrencyException>(() => store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, eventId))));
        }

        [Fact]
        public void FailsOnDuplicateEventId_InSameCommit()
        {
            var eventId = Guid.NewGuid();

            Should.Throw<ConcurrencyException>(() =>
            {
                store.Execute(
                    CreateAppendEventCommand(CreateEventData("stream-1", 0, eventId)),
                    CreateAppendEventCommand(CreateEventData("stream-2", 0, eventId)));
            });
        }

        [Fact]
        public void FailsIfSequenceNumberIsAny()
        {
            Should.Throw<InvalidOperationException>(() => store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", SequenceNumber.Any))));
        }

        [Fact]
        public void FailsIfSequenceNumberIsNegative()
        {
            Should.Throw<InvalidOperationException>(() => store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", SequenceNumber.Any - 1))));
        }

        [Fact]
        public void FailsOnDuplicateSequenceNumberInStream()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));

            Should.Throw<ConcurrencyException>(() => store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0))));
        }

        [Fact]
        public void AppendsInTransactionIsAtomic()
        {
            store.Transactionally(tx =>
            {
                store.Execute(Enumerable.Range(0, 1000).Select(i =>
                    CreateAppendEventCommand(CreateEventData("stream-0", i))));

                try
                {
                    // save a duplicate
                    store.Execute(CreateAppendEventCommand(CreateEventData("stream-0", 0)));
                }
                catch (Exception)
                {
                    // suppress
                }
            });

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0)).ToList(), IsolationLevel.Snapshot);

            events.ShouldBeEmpty();
        }
    }
}