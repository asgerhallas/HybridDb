using System;
using Microsoft.Data.SqlClient;
using Dapper;

namespace HybridDb.Queue
{
    public class EnqueueCommand : Command<string>
    {
        public const string DefaultTopic = "default";

        public EnqueueCommand(QueueTable table, HybridDbMessage message, Func<object, Guid, string> idGenerator = null)
        {
            if (idGenerator == null && message.Id == null)
                throw new ArgumentException("Message id was null and no id generator was provided.");
            if (message.Topic != null && string.IsNullOrWhiteSpace(message.Topic))
                throw new ArgumentException("Message topic can not be empty or whitespace only.");

            Table = table;
            Message = message with { Topic = message.Topic ?? DefaultTopic };
            IdGenerator = idGenerator;
        }

        public QueueTable Table { get; }
        public Func<object, Guid, string> IdGenerator { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tableName = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);
            var discriminator = tx.Store.Configuration.TypeMapper.ToDiscriminator(command.Message.Payload.GetType());

            var id = command.IdGenerator?.Invoke(command.Message.Payload, tx.CommitId) ??
                     command.Message.Id;

            command.Message.Metadata[HybridDbMessage.EnqueuedAtKey] = DateTimeOffset.Now.ToString("O");

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tableName} (Topic, Version, Id, [Order], CommitId, Discriminator, Message, Metadata) 
                    values (@Topic, @Version, @Id, @Order, @CommitId, @Discriminator, @Message, @Metadata); 
                    set nocount off;",
                    new
                    {
                        command.Message.Topic,
                        Version = options.Version.ToString(),
                        Id = id,
                        command.Message.Order,
                        tx.CommitId,
                        Discriminator = discriminator,
                        Message = serializer(command.Message.Payload),
                        Metadata = serializer(command.Message.Metadata)
                    },
                    tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // Enqueuing is idempotent. It should ignore exceptions from primary key or unique index violations.
                if (e.Number is 2627 or 2601) return id;

                throw;
            }

            return id;
        }
    }
}