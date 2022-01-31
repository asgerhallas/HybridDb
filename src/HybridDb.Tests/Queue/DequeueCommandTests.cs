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
        }

        [Fact]
        public void IdAndTopic_RowDataHasPrecedeceOverDocument()
        {
            UseMessageQueue();

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

        [Fact]
        public void Version_OldStore_DoesNotReadNewerMessages()
        {
            UseMessageQueue();

            // Simulate another server running an older version
            var oldStore = store;

            ResetConfiguration();
            UseMessageQueue();
            UseMigrations(new InlineMigration(1));

            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a", "text")));

            var message = (MyMessage)oldStore.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.ShouldBe(null);
        }

        [Fact]
        public void Version_OldStore_ReadsOldMessages()
        {
            UseMessageQueue();

            // Simulate another server running an older version
            var oldStore = store;

            oldStore.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a", "text")));

            ResetConfiguration();
            UseMessageQueue();
            UseMigrations(new InlineMigration(1));

            var message = (MyMessage)oldStore.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Id.ShouldBe("a");
        }

        [Fact]
        public void Version_NewStore_ReadsOldMessages()
        {
            UseMessageQueue();

            store.Execute(new EnqueueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new MyMessage("a", "text")));

            ResetConfiguration();
            UseMessageQueue();
            UseMigrations(new InlineMigration(1));

            var message = (MyMessage)store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Id.ShouldBe("a");
        }

        [Fact]
        public void Version_MigratesOldStore()
        {
            var tableName = store.Database.FormatTableName("messages");

            store.Database.RawExecute($@"
                if (object_id('{tableName}', 'U') is null)
                begin
                    CREATE TABLE [dbo].[{tableName}] (
                        [Topic] [nvarchar](850) NOT NULL,
	                    [Id] [nvarchar](850) NOT NULL,
	                    [CommitId] [uniqueidentifier] NOT NULL,
	                    [Discriminator] [nvarchar](850) NOT NULL,
	                    [Message] [nvarchar](max) NULL,

                        CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([Topic] ASC, [Id] ASC)
                    )
                end;

                insert into [{tableName}] (Topic, Id, CommitId, Discriminator, Message) values (
                    'default',
                    'a',
                    '82ECD1E8-AFB0-44C5-B7FC-A4D70AE1D5B9',
                    'DequeueCommandTests+MyMessage', 
                    '{{ ""Id"": ""a"", ""Text"": ""text""}}')

                ", schema: true);

            ResetConfiguration();
            UseMessageQueue();

            var message = (MyMessage)store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" }));

            message.Id.ShouldBe("a");
        }


        public record MyMessage(string Id, string Text) : HybridDbMessage(Id);
    }
}