using System;
using System.Linq;
using Dapper;
using HybridDb.Config;

namespace HybridDb.Queue
{
    public class DequeueCommand : Command<HybridDbMessage>
    {
        public DequeueCommand(QueueTable table) => Table = table;

        public QueueTable Table { get; }

        public static HybridDbMessage Execute(Func<string, Type, object> deserializer, DocumentTransaction tx, DequeueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var msg = (tx.SqlConnection.Query<(string Message, string Discriminator)>(@$"
                    set nocount on; 
                    delete top(1) from {tablename} with (rowlock, readpast) 
                    output deleted.Message, deleted.Discriminator where IsFailed = 0; 
                    set nocount off;",
                null, tx.SqlTransaction
            )).SingleOrDefault();

            if (msg == default) return null;

            var type = tx.Store.Configuration.TypeMapper.ToType(msg.Discriminator);

            return (HybridDbMessage)deserializer(msg.Message, type);
        }
    }
}