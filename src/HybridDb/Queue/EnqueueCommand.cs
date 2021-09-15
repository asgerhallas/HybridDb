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
            Message = message;
            Topic = topic;
        }

        public QueueTable Table { get; }
        public string Topic { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var topic = command.Topic ?? command.Message.Topic ?? DefaultTopic;
            
            // Add the Topic to the message, so it's present when deserialized
            var message = command.Message with {Topic = topic};

            var discriminator = tx.Store.Configuration.TypeMapper.ToDiscriminator(message.GetType());

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tablename} (Topic, Id, CommitId, Discriminator, Message) 
                    values (@Topic, @Id, @CommitId, @Discriminator, @Message); 
                    set nocount off;",
                    new
                    {
                        Topic = topic,
                        message.Id,
                        tx.CommitId,
                        Discriminator = discriminator,
                        Message = serializer(message)
                    },
                    tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // Enqueuing is idempotent. It should ignore exceptions from primary key violations and just not insert the message.
                if (e.Number == 2627) return message.Id;

                throw;
            }

            return message.Id;
        }
    }
}