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
    }
}