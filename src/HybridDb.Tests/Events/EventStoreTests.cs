using System;
using HybridDb.Events;
using HybridDb.Events.Commands;

namespace HybridDb.Tests.Events
{
    public class EventStoreTests : HybridDbAutoInitializeTests
    {
        protected static AppendEvent CreateAppendEventCommand(EventData<byte[]> eventData) => new AppendEvent(new EventTable("events"), "1.0", eventData);

        protected static EventData<byte[]> CreateEventData(string streamId, long sequenceNumber, string name = "myevent") => 
            CreateEventData(streamId, sequenceNumber, Guid.NewGuid(), name);

        protected static EventData<byte[]> CreateEventData(string streamId, long sequenceNumber, Guid eventId, string name = "myevent") =>
            new EventData<byte[]>(streamId, eventId, name, sequenceNumber, new Metadata(), new[] {(byte) sequenceNumber});
    }
}