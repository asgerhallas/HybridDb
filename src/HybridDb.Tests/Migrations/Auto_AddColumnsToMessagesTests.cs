using System;
using HybridDb.Queue;
using HybridDb.SqlBuilder;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations
{
    public class Auto_AddColumnsToMessagesTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void AddVersion()
        {
            var tablename = "messages";
            var table = new QueueTable(tablename);
            var tableNameFormatted = store.Database.FormatTableName(tablename);

            store.Database.RawExecute(Sql.From($@"
                if (object_id('{tableNameFormatted:@}', 'U') is null)
                begin
                    CREATE TABLE [dbo].[{tableNameFormatted:@}] (
                        [Topic] [nvarchar](850) NOT NULL,
	                    [Id] [nvarchar](850) NOT NULL,
	                    [CommitId] [uniqueidentifier] NOT NULL,
	                    [Discriminator] [nvarchar](850) NOT NULL,
	                    [Message] [nvarchar](max) NULL,

                        CONSTRAINT [PK_{tableNameFormatted:@}] PRIMARY KEY CLUSTERED ([Topic] ASC, [Id] ASC)
                    )
                end"), schema: true);

            var topic = "default";
            var id = Guid.NewGuid().ToString();
            var commitId = Guid.NewGuid().ToString();
            var discriminator = configuration.TypeMapper.ToDiscriminator(typeof(Message));
            var message = configuration.Serializer.Serialize(new Message());

            store.Database.RawExecute(Sql.From($@"
                insert into {table} 
                (Topic, Id, CommitId, Discriminator, Message)
                values ({topic}, {id}, {commitId}, {discriminator}, {message});"));

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.0")
            });

            TouchStore();

            var oldMessage = store.Execute(new DequeueCommand(table, ["default"]));

            oldMessage.ShouldNotBe(null);
        }
        
        [Fact]
        public void AddMetadata()
        {
            var tablename = "messages";
            var table = new QueueTable(tablename);
            var tableNameFormatted = store.Database.FormatTableName(tablename);

            store.Database.RawExecute(Sql.From($@"
                if (object_id('{tableNameFormatted:@}', 'U') is null)
                begin
                    CREATE TABLE [dbo].[{tableNameFormatted:@}] (
                        [Topic] [nvarchar](850) NOT NULL,
	                    [Id] [nvarchar](850) NOT NULL,
	                    [CommitId] [uniqueidentifier] NOT NULL,
	                    [Discriminator] [nvarchar](850) NOT NULL,
	                    [Message] [nvarchar](max) NULL,

                        CONSTRAINT [PK_{tableNameFormatted:@}] PRIMARY KEY CLUSTERED ([Topic] ASC, [Id] ASC)
                    )
                end"), schema: true);

            var topic = "default";
            var id = Guid.NewGuid().ToString();
            var commitId = Guid.NewGuid().ToString();
            var discriminator = configuration.TypeMapper.ToDiscriminator(typeof(Message));
            var message = configuration.Serializer.Serialize(new Message());

            store.Database.RawExecute(Sql.From($@"
                insert into {table} 
                (Topic, Id, CommitId, Discriminator, Message)
                values ({topic}, {id}, {commitId}, {discriminator}, {message});"));

            ResetConfiguration();

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.0")
            });

            TouchStore();

            var oldMessage = store.Execute(new DequeueCommand(table, ["default"]));
            oldMessage.Metadata.ShouldNotBe(null);

            store.Execute(new EnqueueCommand(table, new HybridDbMessage("a", "payload")
            {
                Metadata = { ["meta"] = "facebook" }
            }));

            var newMessage = store.Execute(new DequeueCommand(table, ["default"]));
            newMessage.Metadata.ShouldContainKeyAndValue("meta", "facebook");
        }

        public record Message();
    }
}