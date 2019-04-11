using System;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class GetPositionTests : EventStoreTests
    {
        public GetPositionTests()
        {
            UseEventStore();
        }

        [Fact]
        public void GetPosition()
        {
            var commitId = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("some-id", 0)));
                tx.Execute(CreateAppendEventCommand(CreateEventData("another-id", 0)));
                return tx.CommitId;
            });

            store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("some-id", 1)));
                tx.Execute(CreateAppendEventCommand(CreateEventData("another-id", 1)));
            });

            var position = store.Transactionally(x => x.Execute(new GetPosition(new EventTable("events"), commitId)));
            position.ShouldBe(new Position(0, 1));
        }

        [Fact]
        public void GetPositionOfNonExistingCommit()
        {
            var position = store.Transactionally(x => x.Execute(new GetPosition(new EventTable("events"), Guid.NewGuid())));
            position.ShouldBe(new Position(-1L, -1L));
        }
    }
}