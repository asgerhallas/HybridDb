using System;
using System.Data;
using System.Linq;
using Dapper;

namespace HybridDb.Queue
{
    public class ReadMessageStatsCommand : Command<int>
    {
        public ReadMessageStatsCommand(QueueTable table)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public QueueTable Table { get; }

        public static int Execute(DocumentTransaction tx, ReadMessageStatsCommand command)
        {
            if (tx.SqlTransaction.IsolationLevel != IsolationLevel.Snapshot)
            {
                throw new InvalidOperationException("Reading stats from the queue is best done in snapshot isolation so it doesn't block.");
            }

            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var msg = (tx.SqlConnection.Query<int>(@$"
                    set nocount on; 
                    select count(*) from {tablename}
                    set nocount off;",
                new
                {
                    Version = options.Version.ToString()
                }, 
                tx.SqlTransaction
            )).SingleOrDefault();

            return msg;
        }
    }
}