using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Dapper;

namespace HybridDb.Queue
{
    public class EnqueueCommand : Command<string>
    {
        public const string DefaultTopic = "default";

        public EnqueueCommand(QueueTable table, HybridDbMessage message, string topic = null)
        {
            Table = table;
            Message = message with { Topic = topic ?? message.Topic ?? DefaultTopic };

            if (string.IsNullOrWhiteSpace(Message.Topic)) throw new ArgumentException("Message topic can not be empty string.");
        }

        public QueueTable Table { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);
            var discriminator = tx.Store.Configuration.TypeMapper.ToDiscriminator(command.Message.GetType());

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tablename} (Topic, Version, Id, CommitId, Discriminator, Message) 
                    values (@Topic, @Version, @Id, @CommitId, @Discriminator, @Message); 
                    set nocount off;",
                    new
                    {
                        command.Message.Topic,
                        Version = options.Version.ToString(),
                        command.Message.Id,
                        tx.CommitId,
                        Discriminator = discriminator,
                        Message = serializer(command.Message)
                    },
                    tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // Enqueuing is idempotent. It should ignore exceptions from primary key violations and just not insert the message.
                if (e.Number == 2627) return command.Message.Id;

                throw;
            }

            return command.Message.Id;
        }
    }
}