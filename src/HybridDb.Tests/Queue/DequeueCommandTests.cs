using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Queue;
using HybridDb.SqlBuilder;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Queue
{
    public class DequeueCommandTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void IdAndTopic_RowDataHasPrecedeceOverDocument()
        {
            configuration.UseMessageQueue(new MessageQueueOptions());

            using (var session = store.OpenSession())
            {
                session.Enqueue("MyId", new MyMessage("Text"), "MyTopic");
                session.SaveChanges();
            }

            // Manipulate the topic directly in database - like returning errors to queue
            var queueTable = store.Configuration.Tables.Values.OfType<QueueTable>().Single();
            store.Database.RawExecute(Sql.From($"update {queueTable} set [Id] = 'OtherId', [Topic] = 'OtherTopic'"));

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> {"MyTopic", "OtherTopic"}));

            message.Id.ShouldBe("OtherId");
            message.Topic.ShouldBe("OtherTopic");
            ((MyMessage)message.Payload).Text.ShouldBe("Text");
        }

        [Fact]
        public void MultipleTopics_DequeuesFromAllSpecifiedTopics()
        {
            configuration.UseMessageQueue(new MessageQueueOptions());

            using (var session = store.OpenSession())
            {
                session.Enqueue("Id1", new MyMessage("Text1"), "TopicA");
                session.Enqueue("Id2", new MyMessage("Text2"), "TopicB");
                session.SaveChanges();
            }

            var queueTable = store.Configuration.Tables.Values.OfType<QueueTable>().Single();
            var topics = new List<string> { "TopicA", "TopicB" };

            var first = store.Execute(new DequeueCommand(queueTable, topics));
            var second = store.Execute(new DequeueCommand(queueTable, topics));
            var third = store.Execute(new DequeueCommand(queueTable, topics));

            first.ShouldNotBeNull();
            second.ShouldNotBeNull();
            third.ShouldBeNull();

            new[] { first.Id, second.Id }.ShouldContain("Id1");
            new[] { first.Id, second.Id }.ShouldContain("Id2");
        }

        [Theory]
        [InlineData("1.2.3", "1.2.3", true)]
        [InlineData("1.2.4", "1.2.3", false)]
        [InlineData("1.3.0", "1.2.3", false)]
        [InlineData("1.0", "1.0", true)]
        [InlineData("1.0", "2.0", true)]
        [InlineData("1.0", "1.1", true)]
        [InlineData("2.0", "1.0", false)]
        [InlineData("2.0", "1.99", false)]
        [InlineData("1.0", "1.0.1", true)]
        [InlineData("1.0.1", "1.0", false)]
        [InlineData("1.0.0", "1.0", false)]
        public void DequeueVersion(string messageVersion, string serverVersion, bool shouldDequeue)
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version(messageVersion)
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue("MyId", new MyMessage("Text"));
                session.SaveChanges();
            }

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version(serverVersion)
            });

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { EnqueueCommand.DefaultTopic }));

            if (shouldDequeue) message.ShouldNotBe(null);
            else message.ShouldBe(null);
        }

        public record MyMessage(string Text);
    }
}