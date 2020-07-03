using System.Data;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Events
{
    public class DeleteStreamTests : EventStoreTests
    {
        public DeleteStreamTests(ITestOutputHelper output) : base(output)
        {
            UseEventStore();
        }

        [Fact]
        public void Delete()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)),
                CreateAppendEventCommand(CreateEventData("stream-2", 0)));

            var rowsDeleted = Execute(new DeleteStream(new EventTable("events"), "stream-1"));

            rowsDeleted.ShouldBe(2);

            store.Transactionally(IsolationLevel.Snapshot, tx => tx
                .Execute(new ReadStream(new EventTable("events"), "stream-1", 0))
            ).ShouldBeEmpty();
        }

        int Execute(DeleteStream command) => store.Transactionally(IsolationLevel.Snapshot, tx => tx.Execute(command));
    }
}