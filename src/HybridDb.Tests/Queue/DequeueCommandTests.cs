using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Queue
{
    public class DequeueCommandTests : HybridDbTests
    {
        public DequeueCommandTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void IdAndTopic_RowDataHasPrecedeceOverDocument()
        {
            configuration.UseMessageQueue(new MessageQueueOptions());

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("MyId", "Text"), "MyTopic");
                session.SaveChanges();
            }

            // Manipulate the topic directly in database - like returning errors to queue
            var queueTable = store.Configuration.Tables.Values.OfType<QueueTable>().Single();
            store.Database.RawExecute($"update {store.Database.FormatTableNameAndEscape(queueTable.Name)} set [Id] = 'OtherId', [Topic] = 'OtherTopic'");

            var message = (MyMessage) store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> {"MyTopic", "OtherTopic"}));

            message.Id.ShouldBe("OtherId");
            message.Topic.ShouldBe("OtherTopic");
            message.Text.ShouldBe("Text");
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
                session.Enqueue(new MyMessage("MyId", "Text"));
                session.SaveChanges();
            }

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version(serverVersion)
            });

            var message = (MyMessage) store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { EnqueueCommand.DefaultTopic }));

            if (shouldDequeue) message.ShouldNotBe(null);
            else message.ShouldBe(null);
        }

        public record MyMessage(string Id, string Text) : HybridDbMessage(Id);
    }
}