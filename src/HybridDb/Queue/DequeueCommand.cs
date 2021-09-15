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

            var msg = (tx.SqlConnection.Query<(string Message, string Discriminator)>(@$"
                    set nocount on; 
                    delete top(1) from {tablename} with (rowlock, readpast) 
                    output deleted.Message, deleted.Discriminator 
                    where Topic in @Topics;
                    set nocount off;",
                new { command.Topics }, 
                tx.SqlTransaction
            )).SingleOrDefault();

            if (msg == default) return null;

            var type = tx.Store.Configuration.TypeMapper.ToType(typeof(HybridDbMessage), msg.Discriminator);

            return (HybridDbMessage)deserializer(msg.Message, type);
        }
    }
}