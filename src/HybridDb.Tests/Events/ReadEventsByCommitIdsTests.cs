using System;
using System.Data;
using System.Linq;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class ReadEventsByCommitIdsTests : EventStoreTests
    {
        public ReadEventsByCommitIdsTests()
        {
            UseEventStore();
        }

        [Fact]
        public void LoadCommits()
        {
            var id1 = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("some-id", 0)));
                return tx.CommitId;
            });

            var id2 = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("some-id", 1)));
                return tx.CommitId;
            });

            var commits = store.Transactionally(tx => tx.Execute(new ReadEventsByCommitIds(new EventTable("events"), id1, id2)).ToList(), IsolationLevel.Snapshot);
            commits.Count.ShouldBe(2);
            commits[0].Id.ShouldBe(id1);
            commits[1].Id.ShouldBe(id2);
        }

        [Fact]
        public void LoadEmptyListOfCommits()
        {
            var commits = store.Transactionally(tx => tx.Execute(new ReadEventsByCommitIds(new EventTable("events"))).ToList(), IsolationLevel.Snapshot);
            commits.ShouldBeEmpty();
        }

        [Fact]
        public void LoadEmptyCommitFromEmptyGuid()
        {
            var commits = store.Transactionally(tx => tx.Execute(new ReadEventsByCommitIds(new EventTable("events"), Guid.Empty)).ToList(), IsolationLevel.Snapshot);

            commits.Count.ShouldBe(1);
            commits[0].Id.ShouldBe(Guid.Empty);
            commits[0].Generation.ShouldBe("1.0");
            commits[0].Begin.ShouldBe(-1);
            commits[0].End.ShouldBe(-1);
        }

        [Fact]
        public void LoadCommitsIdNotFound()
        {
            var id1 = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("some-id", 0)));
                return tx.CommitId;
            });

            var id2 = Guid.NewGuid();

            var commits = store.Transactionally(tx => tx.Execute(new ReadEventsByCommitIds(new EventTable("events"), id1, id2)).ToList(), IsolationLevel.Snapshot);

            commits.Count.ShouldBe(2);
            commits[0].Id.ShouldBe(id1);
            commits[1].ShouldBe(null);
        }
    }
}