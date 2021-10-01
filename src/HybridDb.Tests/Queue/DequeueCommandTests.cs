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
        public DequeueCommandTests(ITestOutputHelper output) : base(output)
        {
            configuration.UseMessageQueue();
        }

        [Fact]
        public void IdAndTopic_RowDataHasPrecedeceOverDocument()
        {
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

        public record MyMessage(string Id, string Text) : HybridDbMessage(Id);
    }
}