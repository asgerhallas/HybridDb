using System;
using System.Collections.Generic;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Queue
{
    public class QueueExTests : HybridDbTests
    {
        public QueueExTests(ITestOutputHelper output) : base(output)
        {
            configuration.UseMessageQueue();
        }

        public record MyMessage(string Text);

        [Fact]
        public void Enqueue_WithDefaultOrderFromContext()
        {
            using var session = store.OpenSession();

            session.SetDefaultMessageOrder(9);

            session.Enqueue(new MyMessage("Some command")).Order.ShouldBe(9);
            session.Enqueue("id", new MyMessage("Some command")).Order.ShouldBe(9);
        }

        [Fact]
        public void Enqueue_ClearDefaultOrderFromContext()
        {
            using var session = store.OpenSession();

            session.SetDefaultMessageOrder(9);
            session.ClearDefaultMessageOrder();

            session.Enqueue(new MyMessage("Some command")).Order.ShouldBe(0);
            session.Enqueue("id", new MyMessage("Some command")).Order.ShouldBe(0);
        }

        [Fact]
        public void Enqueue_WithDefaultOrder()
        {
            using var session = store.OpenSession();

            session.Enqueue(new MyMessage("Some command")).Order.ShouldBe(0);
            session.Enqueue("id", new MyMessage("Some command")).Order.ShouldBe(0);
        }

        [Fact]
        public void Enqueue_WithGivenOrder()
        {
            using var session = store.OpenSession();

            // The given order has precedence, so this is ignored
            session.SetDefaultMessageOrder(9);

            session.Enqueue(new MyMessage("Some command"), order: 25).Order.ShouldBe(25);
            session.Enqueue("id", new MyMessage("Some command"), order: 26).Order.ShouldBe(26);
        }

        [Fact]
        public void GetDefaultMessageOrder()
        {
            using var session = store.OpenSession();

            session.GetDefaultMessageOrder().ShouldBe(0);

            session.SetDefaultMessageOrder(123);

            session.GetDefaultMessageOrder().ShouldBe(123);
        }

        [Fact]
        public void Enqueue_MultipleTopics_ReturnsOneMessagePerTopic()
        {
            using var session = store.OpenSession();

            var results = session.Enqueue(new MyMessage("hello"), new List<string> { "topic-a", "topic-b" });

            results.Count.ShouldBe(2);
            results[0].Topic.ShouldBe("topic-a");
            results[1].Topic.ShouldBe("topic-b");
        }

        [Fact]
        public void Enqueue_MultipleTopics_WithId_ReusesSameId()
        {
            using var session = store.OpenSession();

            var results = session.Enqueue("my-id", new MyMessage("hello"), new List<string> { "topic-a", "topic-b" });

            results.Count.ShouldBe(2);
            results[0].Id.ShouldBe("my-id");
            results[1].Id.ShouldBe("my-id");
            results[0].Topic.ShouldBe("topic-a");
            results[1].Topic.ShouldBe("topic-b");
        }

        [Fact]
        public void Enqueue_MultipleTopics_NullTopics_Throws()
        {
            using var session = store.OpenSession();

            Should.Throw<ArgumentNullException>(() => session.Enqueue(new MyMessage("hello"), (IReadOnlyList<string>)null));
        }

        [Fact]
        public void Enqueue_MultipleTopics_EmptyTopics_EnqueuesToDefaultTopic()
        {
            using var session = store.OpenSession();

            var results = session.Enqueue(new MyMessage("hello"), new List<string>());

            results.Count.ShouldBe(1);
            results[0].Topic.ShouldBeNull();
        }

        [Fact]
        public void Enqueue_MultipleTopics_MetadataIsIsolatedPerTopic()
        {
            using var session = store.OpenSession();

            var metadata = new Dictionary<string, string> { ["key"] = "value" };

            var results = session.Enqueue(new MyMessage("hello"), new List<string> { "topic-a", "topic-b" }, metadata: metadata);

            results.Count.ShouldBe(2);
            results[0].Metadata.ShouldNotBeSameAs(results[1].Metadata);
            results[0].Metadata["key"].ShouldBe("value");
            results[1].Metadata["key"].ShouldBe("value");
        }
    }
}