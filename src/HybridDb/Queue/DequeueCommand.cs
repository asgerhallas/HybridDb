using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace HybridDb.Queue
{
    public class DequeueCommand : Command<HybridDbMessage>
    {
        public DequeueCommand(QueueTable table, IReadOnlyList<string> topics)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            
            if (topics == null) throw new ArgumentNullException(nameof(topics));
            if (topics.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(topics));

            Topics = topics;
        }

        public QueueTable Table { get; }
        public IReadOnlyList<string> Topics { get; }

        public static HybridDbMessage Execute(Func<string, Type, object> deserializer, DocumentTransaction tx, DequeueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var msg = (tx.SqlConnection.Query<(string Id, string Message, string Discriminator, string Topic)>(@$"
                    set nocount on; 
                    delete top(1) from {tablename} with (rowlock, readpast) 
                    output deleted.Id, deleted.Message, deleted.Discriminator, deleted.Topic
                    where Topic in @Topics and Version <= @ConfiguredVersion;
                    set nocount off;",
                new { command.Topics, tx.Store.Configuration.ConfiguredVersion }, 
                tx.SqlTransaction
            )).SingleOrDefault();

            if (msg == default) return null;

            var type = tx.Store.Configuration.TypeMapper.ToType(typeof(HybridDbMessage), msg.Discriminator);

            return (HybridDbMessage) deserializer(msg.Message, type) with
            {
                // If someone has written directly to the row in Sql Server, the row info takes precedence
                Id = msg.Id,
                Topic = msg.Topic
            };
        }
    }
}