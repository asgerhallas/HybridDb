using System.Collections.Generic;
using System.Diagnostics.Tracing;
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
            configuration.UseMessageQueue(new MessageQueueOptions());
        }

        [Fact]
        public void Topic_NotSet()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("a", new MyMessage())));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Topic.ShouldBe("default");
        }

        [Fact]
        public void Topic_SetByMessage()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("a", new MyMessage(), "TopicA")));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "TopicA" }));

            message.Topic.ShouldBe("TopicA");
        }

        [Fact]
        public void IdGenerator()
        {
            var resultingId = store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("a", new MyMessage())
                {
                    IdGenerator = commitId => commitId.ToString()
                }));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Id.ShouldBe(resultingId);
        }

        public record MyMessage;
    }
}