using System;
using HybridDb.Migrations.Schema;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations
{
    public class Auto_AddVersionsNumbersToMessagesTests : HybridDbTests
    {
        public Auto_AddVersionsNumbersToMessagesTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void AutoMigratesMessageQueueTable()
        {
            var tablename = "messages";
            var tableNameFormatted = store.Database.FormatTableName(tablename);

            store.Database.RawExecute($@"
                if (object_id('{tableNameFormatted}', 'U') is null)
                begin
                    CREATE TABLE [dbo].[{tableNameFormatted}] (
                        [Topic] [nvarchar](850) NOT NULL,
	                    [Id] [nvarchar](850) NOT NULL,
	                    [CommitId] [uniqueidentifier] NOT NULL,
	                    [Discriminator] [nvarchar](850) NOT NULL,
	                    [Message] [nvarchar](max) NULL,

                        CONSTRAINT [PK_{tableNameFormatted}] PRIMARY KEY CLUSTERED ([Topic] ASC, [Id] ASC)
                    )
                end", schema: true);

            store.Database.RawExecute(@$"
                insert into {store.Database.FormatTableNameAndEscape(tablename)} 
                (Topic, Id, CommitId, Discriminator, Message)
                values (@Topic, @Id, @CommitId, @Discriminator, @Message);",
                new
                {
                    Topic = "default",
                    Id = Guid.NewGuid().ToString(),
                    CommitId = Guid.NewGuid().ToString(),
                    Discriminator = configuration.TypeMapper.ToDiscriminator(typeof(Message)),
                    Message = configuration.Serializer.Serialize(new Message())
                });

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.0")
            });

            TouchStore();

            var message = store.Execute(new DequeueCommand(new QueueTable(tablename), new[] {"default"}));

            message.ShouldNotBe(null);
        }

        public record Message() : HybridDbMessage(Guid.NewGuid().ToString(), "default");

        public class CreateQueueTable_v1 : DdlCommand
        {
            public QueueTable QueueTable { get; }

            public CreateQueueTable_v1(QueueTable queueTable)
            {
                Safe = true;
                QueueTable = queueTable;
            }

            public override string ToString() => "Create queue table";

            public override void Execute(DocumentStore store)
            {
                var tableName = store.Database.FormatTableName(QueueTable.Name);

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
                    end", schema: true);
            }
        }
    }
}