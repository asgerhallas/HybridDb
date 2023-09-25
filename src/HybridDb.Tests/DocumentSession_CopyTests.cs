using System;
using System.Collections.Generic;
using System.Reflection;
using HybridDb.Commands;
using HybridDb.Events;
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
            sessionCopy.Advanced.ManagedEntities.Count.ShouldBe(2);
        }

        [Fact]
        public void CopySessionWithEvents()
        {
            using var session = store.OpenSession();

            session.Append(1, new EventData<byte[]>("1", Guid.NewGuid(), "Name1", 1, new Metadata(), new[] { (byte)1 }));
            session.Append(2, new EventData<byte[]>("2", Guid.NewGuid(), "Name2", 2, new Metadata(), new[] { (byte)2 }));

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.ShouldBeLike(session);

            var copiedEvents = (List<(int Generation, EventData<byte[]> Data)>)sessionCopy
                .GetType()
                .GetField("events", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sessionCopy);
            copiedEvents.Count.ShouldBe(2);
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

            sessionCopy.Advanced.DeferredCommands.Count.ShouldBe(2);
        }

        [Fact]
        public void CopySessionWithEnlistedTx()
        {
            using var session = store.OpenSession();
            using var tx = store.BeginTransaction();

            session.Advanced.Enlist(tx);

            session.Advanced.DocumentTransaction.Execute(new InsertCommand(
                store.Configuration.GetDesignFor<LocalEntity>().Table,
                $"{Guid.NewGuid()}",
                new { Field = "1" }));

            session.Advanced.DocumentTransaction.Execute(new InsertCommand(
                store.Configuration.GetDesignFor<LocalEntity>().Table,
                $"{Guid.NewGuid()}",
                new { Field = "1" }));

            var sessionCopy = session.Advanced.Copy();

            sessionCopy.Advanced.DocumentTransaction.ShouldNotBeNull();
        }

        public class LocalEntity
        {
            public string Id { get; set; }
            public string SomeData { get; set; }
            public int SomeNumber { get; set; }
        }
    }
}