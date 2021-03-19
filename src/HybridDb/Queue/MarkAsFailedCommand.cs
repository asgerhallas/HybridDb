using Dapper;

namespace HybridDb.Queue
{
    public class MarkAsFailedCommand : Command<int>
    {
        public MarkAsFailedCommand(QueueTable table, string messageId)
        {
            Table = table;
            MessageId = messageId;
        }

        public QueueTable Table { get; }
        public string MessageId { get; }

        public static int Execute(DocumentTransaction tx, MarkAsFailedCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            return tx.SqlConnection.Execute(
                $"update {tablename} set IsFailed = 1 where Id = @MessageId", 
                new { command.MessageId }, 
                tx.SqlTransaction);
        }
    }
}