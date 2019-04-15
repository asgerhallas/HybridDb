using System;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class LoadParentCommitTests : EventStoreTests
    {
        public LoadParentCommitTests()
        {
            UseEventStore();
        }

        [Fact]
        public void LoadParentCommit()
        {
            var parentCommitId = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
                return tx.CommitId;
            });

            var childCommitId = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1)));
                return tx.CommitId;
            });

            var parentCommit = store.Execute(new LoadParentCommit(new EventTable("events"), childCommitId));

            parentCommit.Id.ShouldBe(parentCommitId);
            parentCommit.Begin.ShouldBe(0);
            parentCommit.End.ShouldBe(0);
        }

        [Fact]
        public void LoadFirstCommitsParentCommit()
        {
            var childCommitId = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
                return tx.CommitId;
            });

            var commit = store.Execute(new LoadParentCommit(new EventTable("events"), childCommitId));
            commit.Id.ShouldBe(Guid.Empty);
            commit.Begin.ShouldBe(-1);
            commit.End.ShouldBe(-1);
        }

        [Fact]
        public void LoadNonExistingCommitsParentCommit()
        {
            var commitId = store.Transactionally(tx =>
            {
                tx.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
                return tx.CommitId;
            });

            var commit = store.Execute(new LoadParentCommit(new EventTable("events"), Guid.NewGuid()));

            commit.Id.ShouldBe(commitId);
            commit.Begin.ShouldBe(0);
            commit.End.ShouldBe(0);
        }

        [Fact]
        public void LoadEmptyCommitsParent()
        {
            store.Execute(new LoadParentCommit(new EventTable("events"), Guid.Empty)).ShouldBe(null);
        }
    }
}