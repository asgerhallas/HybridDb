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

            var @event = store.Transactionally(IsolationLevel.Snapshot, tx =>
                tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0))
                    .SelectMany(x => x.Events)
                    .Single());

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

            var events = store.Transactionally(IsolationLevel.Snapshot, tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0)).ToList());

            events.ShouldBeEmpty();
        }

        [Fact]
        public void SequenceNumberAny_BeginningOfStream()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                .Event.SequenceNumber.ShouldBe(0);
        }

        [Fact]
        public void SequenceNumberAny_ExistingStream()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)),
                CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                .Event.SequenceNumber.ShouldBe(3);
        }

        [Fact]
        public void SequenceNumberAny_MultipleInSameTx()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)));

            store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(2);

                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(3);
            });
        }

        [Fact]
        public void SequenceNumberAny_MixedWithAssignedSequenceNumbers()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)));

            store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(2);

                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 3)))
                    .Event.SequenceNumber.ShouldBe(3);

                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(4);
            });
        }

        [Fact]
        public void SequenceNumberAny_OnMultipleStreams()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-2", 0)));

            store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(1);

                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-2", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(1);

                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-3", SequenceNumber.Any)))
                    .Event.SequenceNumber.ShouldBe(0);
            });
        }

    }
}