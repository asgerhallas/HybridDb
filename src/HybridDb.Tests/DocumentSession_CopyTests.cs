using System;
using System.Collections.Generic;
using System.Reflection;
using HybridDb.Commands;
using HybridDb.Events;
using HybridDb.Queue;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_CopyTests : HybridDbTests
    {
        public DocumentSession_CopyTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void CopySession()
        {
            using var session = store.OpenSession();

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
        }

        [Fact]
        public void CopySessionWithEntities()
        {
            using var session = store.OpenSession();

            session.Store(new OtherEntity { Id = "Test string 1", Number = 1 });
            session.Store(new OtherEntity { Id = "Test string 2", Number = 1 });

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
        }

        [Fact]
        public void CopySessionWithEvents()
        {
            using var session = store.OpenSession();

            session.Append(1, new EventData<byte[]>("1", Guid.NewGuid(), "Name1", 1, new Metadata(), new[] { (byte)1 }));

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
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
        }

        [Fact]
        public void CopySessionWithEnlistedTx()
        {
            using var session = store.OpenSession();
            using var tx = store.BeginTransaction();

            session.Advanced.Enlist(tx);

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.Advanced.DocumentTransaction.ShouldBe(tx);
        }

        [Fact]
        public void CopySessionWithSessionData()
        {
            using var session = store.OpenSession();

            var context = new MessageContext(new HybridDbMessage($"{Guid.NewGuid()}", "dbMessage"));

            session.Advanced.SessionData.Add(MessageContext.Key, context.IncomingMessage.Id);

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);
        }

        public class LocalEntity
        {
            public string Id { get; set; }
            public string SomeData { get; set; }
            public int SomeNumber { get; set; }
        }
    }
}