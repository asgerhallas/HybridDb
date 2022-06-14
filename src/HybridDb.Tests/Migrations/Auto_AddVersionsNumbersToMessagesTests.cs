using System;
using System.Collections.Generic;
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
        public void AutoMigratesMessageQueueTable_AddVersion()
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
                    Message = configuration.Serializer.Serialize(new Message()),
                });

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.0")
            });

            TouchStore();

            var oldMessage = store.Execute(new DequeueCommand(new QueueTable(tablename), new[] {"default"}));

            oldMessage.ShouldNotBe(null);
        }
        
        [Fact]
        public void AutoMigratesMessageQueueTable_AddMetadata()
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
                    Message = configuration.Serializer.Serialize(new Message()),
                });

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.0")
            });

            TouchStore();

            var oldMessage = store.Execute(new DequeueCommand(new QueueTable(tablename), new[] {"default"}));
            oldMessage.Metadata.ShouldNotBe(null);

            store.Execute(new EnqueueCommand(new QueueTable(tablename), new HybridDbMessage("a", "payload")
            {
                Metadata = { ["meta"] = "facebook" }
            }));

            var newMessage = store.Execute(new DequeueCommand(new QueueTable(tablename), new[] { "default" }));
            newMessage.Metadata.ShouldContainKeyAndValue("meta", "facebook");
        }

        public record Message();
    }
}