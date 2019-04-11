using System;
using HybridDb.Events;
using HybridDb.Events.Commands;

namespace HybridDb.Tests.Events
{
    public class EventStoreTests : HybridDbAutoInitializeTests
    {
        protected static AppendEventCommand CreateAppendEventCommand(EventData<byte[]> eventData) => new AppendEventCommand(new EventTable("events"), "1.0", eventData);

        protected static EventData<byte[]> CreateEventData(string streamId, int sequenceNumber, string name = "myevent") => 
            CreateEventData(streamId, sequenceNumber, Guid.NewGuid(), name);

        protected static EventData<byte[]> CreateEventData(string streamId, int sequenceNumber, Guid eventId, string name = "myevent") =>
            new EventData<byte[]>(streamId, eventId, name, sequenceNumber, new Metadata(), new[] {(byte) sequenceNumber});
    }
}