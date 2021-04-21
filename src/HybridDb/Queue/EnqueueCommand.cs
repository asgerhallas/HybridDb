using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Dapper;

namespace HybridDb.Queue
{
    public class EnqueueCommand : Command<string>
    {
        static readonly ConcurrentDictionary<Type, string> cache = new();

        public EnqueueCommand(QueueTable table, HybridDbMessage message, string topic = null)
        {
            Table = table;
            Message = message;
            Topic = topic ?? "messages";
        }

        public QueueTable Table { get; }
        public string Topic { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);
            
            var discriminator = cache.GetOrAdd(command.Message.GetType(), key => tx.Store.Configuration.TypeMapper.ToDiscriminator(key));

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tablename} (Topic, Id, CommitId, Discriminator, Message) 
                    values (@Topic, @Id, @CommitId, @Discriminator, @Message); 
                    set nocount off;",
                    new
                    {
                        command.Topic,
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