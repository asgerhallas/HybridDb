using System;
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
        public void MessageId_SetByMessage()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("id_a", new MyMessage(), "DontCare")));

            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("id_b", new MyMessage(), "DontCare")));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                messageId: "id_a"));

            message.Id.ShouldBe("id_a");
        }

        [Fact]
        public void MessageId_NoneMatching()
        {
            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("id_a", new MyMessage(), "DontCare")));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                messageId: "id_b"));

            message.ShouldBe(null);
        }

        [Fact]
        public void IdGenerator()
        {
            var resultingId = store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new HybridDbMessage("a", new MyMessage()), (msg, commitId) => $"{msg.GetType().Name}/{commitId}"));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Id.ShouldBe(resultingId);
        }

        [Fact]
        public void IdGenerator_Idempotency()
        {
            string IdGenerator(object msg, Guid commitId) => $"{msg.GetType().Name}";

            var input = new HybridDbMessage("a", new MyMessage());

            var resultingId1 = store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                input, IdGenerator));

            var resultingId2 = store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                input, IdGenerator));

            var message1 = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            var message2 = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message1.Id.ShouldBe(resultingId1);
            message1.Id.ShouldBe(resultingId2);

            message2.ShouldBe(null);
        }

        public record MyMessage;
    }
}