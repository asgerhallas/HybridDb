using System;
using System.Collections.Generic;
using FakeItEasy;
using HybridDb.Commands;
using HybridDb.Events;
using HybridDb.Queue;
using Microsoft.Extensions.Options;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_CopyTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void CopySession()
        {
            using var session = store.OpenSession();

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
        }

        [Fact]
        public void CopySessionNoAddedToSessionEvent()
        {
            using var session = store.OpenSession();

            session.Store(new OtherEntity { Id = "Test string 1", Number = 1 });

            var list = new List<IHybridDbEvent>();

            configuration.AddEventHandler(@event => list.Add(@event));

            session.Advanced.Copy();

            list.Count.ShouldBe(0);
        }

        [Fact]
        public void CopySessionWithEntities()
        {
            using var session = store.OpenSession();

            session.Store(new OtherEntity { Id = "Test string 1", Number = 1 });
            session.Store(new OtherEntity { Id = "Test string 2", Number = 1 });

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
            sessionCopy.Advanced.ManagedEntities.ShouldBeLike(session.Advanced.ManagedEntities);
            sessionCopy.Advanced.ManagedEntities.ShouldNotBeSameAs(session.Advanced.ManagedEntities);
        }

        [Fact]
        public void CopySessionWithEvents()
        {
            using var session = store.OpenSession();

            session.Append(1, new EventData<byte[]>("1", Guid.NewGuid(), "Name1", 1, new Metadata(), [(byte)1]));

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
            sessionCopy.Advanced.Events.ShouldBeLike(session.Advanced.Events);
            sessionCopy.Advanced.Events.ShouldNotBeSameAs(session.Advanced.Events);
        }

        [Fact]
        public void CopySessionWithDeferredCommands()
        {
            using var session = store.OpenSession();

            session.Advanced.Defer(
                new InsertCommand(
                    store.Configuration.GetDesignFor<LocalEntity>().Table,
                    $"{Guid.NewGuid()}",
                    new { SomeNumber = 1, SomeData = "Data1" }));

            session.Advanced.Defer(
                new InsertCommand(
                    store.Configuration.GetDesignFor<LocalEntity>().Table,
                    $"{Guid.NewGuid()}",
                    new { SomeNumber = 2, SomeData = "Data2" }));

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
            sessionCopy.Advanced.DeferredCommands.ShouldBeLike(session.Advanced.DeferredCommands);
            sessionCopy.Advanced.DeferredCommands.ShouldNotBeSameAs(session.Advanced.DeferredCommands);
        }

        [Fact]
        public void CopySessionWithSessionData()
        {
            using var session = store.OpenSession();

            var context = new MessageContext(new SessionContext(), new HybridDbMessage($"{Guid.NewGuid()}", "dbMessage"));

            session.Advanced.SessionData.Add(MessageContext.Key, context.IncomingMessage.Id);

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
            sessionCopy.Advanced.SessionData.ShouldBeLike(session.Advanced.SessionData);
            sessionCopy.Advanced.SessionData.ShouldNotBeSameAs(session.Advanced.SessionData);
        }

        [Fact]
        public void CopySessionWithEnlistedTx()
        {
            using var session = store.OpenSession();
            using var tx = store.BeginTransaction(session.CommitId);

            session.Advanced.Enlist(tx);

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.Advanced.DocumentTransaction.ShouldBe(tx);
            sessionCopy.CommitId.ShouldBe(session.CommitId);
        }

        public class LocalEntity
        {
            public string Id { get; set; }
            public string SomeData { get; set; }
            public int SomeNumber { get; set; }
        }
    }
}