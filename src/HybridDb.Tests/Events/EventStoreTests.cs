using System;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Xunit.Abstractions;

namespace HybridDb.Tests.Events
{
    public class EventStoreTests : HybridDbTests
    {
        public EventStoreTests(ITestOutputHelper output) : base(output) { }

        protected static AppendEvent CreateAppendEventCommand(EventData<byte[]> eventData) => new AppendEvent(new EventTable("events"), 0, eventData);

        protected static EventData<byte[]> CreateEventData(string streamId, long sequenceNumber, string name = "myevent") => 
            CreateEventData(streamId, sequenceNumber, Guid.NewGuid(), name);

        protected static EventData<byte[]> CreateEventData(string streamId, long sequenceNumber, Guid eventId, string name = "myevent") =>
            new EventData<byte[]>(streamId, eventId, name, sequenceNumber, new Metadata(), new[] {(byte) sequenceNumber});
    }
}