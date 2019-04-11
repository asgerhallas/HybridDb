using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class AppendEventCommandTests : EventStoreTests
    {
        public AppendEventCommandTests()
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
            @event.Data.ShouldBe(new byte[] { 0 });
        }

        [Fact]
        public void FailsOnDuplicateEventId()
        {
            var eventId = Guid.NewGuid();

            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0, eventId)));

            Should.Throw<ConcurrencyException>(() => store.Execute(CreateAppendEventCommand(CreateEventData("stream-2", 0, eventId))));
        }



        [Fact]
        public void StreamsEntireCommitToSubscribtion()
        {
            throw new NotImplementedException();
            var events = new List<EventData>();
            //store.Commits.Subscribe(c => events.AddRange(c.Events));
            //store.Save(EmitMany("stream-1", 0, 1000));

            events.Count.ShouldBe(1000);
        }
    }
}