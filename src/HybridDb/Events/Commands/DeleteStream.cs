using Dapper;

namespace HybridDb.Events.Commands
{
    public class DeleteStream : Command<int>
    {
        public DeleteStream(EventTable table, string streamId)
        {
            Table = table;
            StreamId = streamId;
        }

        public EventTable Table { get; }
        public string StreamId { get; }

        public static int Execute(DocumentTransaction tx, DeleteStream command)
        {
            var sql = $@"
                DELETE FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                WHERE StreamId = @Id";

            // Using DbString over just string as a important performance optimization, 
            // see https://github.com/StackExchange/dapper-dot-net/issues/288
            var idParameter = new DbString {Value = command.StreamId, IsAnsi = false, IsFixedLength = false, Length = 850};

            return tx.SqlConnection.Execute(sql, new {Id = idParameter}, tx.SqlTransaction);
        }
    }
}