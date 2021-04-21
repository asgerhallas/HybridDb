using System;
using System.Collections.Concurrent;
using System.Linq;
using Dapper;

namespace HybridDb.Queue
{
    public class DequeueCommand : Command<HybridDbMessage>
    {
        static readonly ConcurrentDictionary<string, Type> cache = new();

        public DequeueCommand(QueueTable table, string topic = "messages")
        {
            Table = table;
            Topic = topic;
        }

        public QueueTable Table { get; }
        public string Topic { get; }

        public static HybridDbMessage Execute(Func<string, Type, object> deserializer, DocumentTransaction tx, DequeueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var msg = (tx.SqlConnection.Query<(string Message, string Discriminator)>(@$"
                    set nocount on; 
                    delete top(1) from {tablename} with (rowlock, readpast) 
                    output deleted.Message, deleted.Discriminator 
                    where Topic = @Topic;
                    set nocount off;",
                new { command.Topic }, 
                tx.SqlTransaction
            )).SingleOrDefault();

            if (msg == default) return null;

            var type = cache.GetOrAdd(msg.Discriminator, _ => tx.Store.Configuration.TypeMapper.ToType(msg.Discriminator));

            return (HybridDbMessage)deserializer(msg.Message, type);
        }
    }
}