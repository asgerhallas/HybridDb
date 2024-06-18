using System;
using Dapper;
using Microsoft.Data.SqlClient;

namespace HybridDb.Queue
{
    public class EnqueueCommand : Command<string>
    {
        public const string DefaultTopic = "default";

        public EnqueueCommand(QueueTable table, HybridDbMessage message)
        {
            if (message.Id == null)
            {
                throw new ArgumentException("Message id must be set.");
            }

            if (message.Topic != null && string.IsNullOrWhiteSpace(message.Topic))
            {
                throw new ArgumentException("Message topic can not be empty or whitespace only.");
            }

            Table = table;
            Message = message with { Topic = message.Topic ?? DefaultTopic };
        }

        public QueueTable Table { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tableName = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);
            var discriminator = tx.Store.Configuration.TypeMapper.ToDiscriminator(command.Message.Payload.GetType());

            command.Message.Metadata[HybridDbMessage.EnqueuedAtKey] = DateTimeOffset.Now.ToString("O");

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tableName} (Topic, Version, Id, [Order], CommitId, Discriminator, Message, Metadata, CorrelationId) 
                    values (@Topic, @Version, @Id, @Order, @CommitId, @Discriminator, @Message, @Metadata, @CorrelationId); 
                    set nocount off;",
                    new
                    {
                        command.Message.Topic,
                        Version = options.Version.ToString(),
                        command.Message.Id,
                        command.Message.Order,
                        tx.CommitId,
                        Discriminator = discriminator,
                        Message = serializer(command.Message.Payload),
                        Metadata = serializer(command.Message.Metadata),
                        command.Message.CorrelationId
                    },
                    tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // Enqueuing is idempotent. It should ignore exceptions from primary key or unique index violations.
                if (e.Number is 2627 or 2601) return command.Message.Id;

                throw;
            }

            return command.Message.Id;
        }
    }
}