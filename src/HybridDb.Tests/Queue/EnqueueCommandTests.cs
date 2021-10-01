using System.Collections.Generic;
using System.Linq;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Queue
{
    public class EnqueueCommandTests : HybridDbTests
    {
        public EnqueueCommandTests(ITestOutputHelper output) : base(output)
        {
            configuration.UseMessageQueue();
        }

        [Fact]
        public void Topic_NotSet()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a")));

            var message = (MyMessage)store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Topic.ShouldBe("default");
        }

        [Fact]
        public void Topic_SetByMessage()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a") { Topic = "TopicA"}));

            var message = (MyMessage)store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "TopicA" }));

            message.Topic.ShouldBe("TopicA");
        }

        [Fact]
        public void Topic_SetByCtor()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a") { Topic = "TopicA" },
                "TopicB"));

            var message = (MyMessage)store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "TopicB" }));

            message.Topic.ShouldBe("TopicB");
        }

        public record MyMessage(string Id) : HybridDbMessage(Id);
    }
}