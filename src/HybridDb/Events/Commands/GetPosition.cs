using System;
using Dapper;
using HybridDb.Commands;

namespace HybridDb.Events.Commands
{
    public class GetPosition : Command<Position>
    {
        public GetPosition(EventTable table, Guid commitId)
        {
            Table = table;
            CommitId = commitId;
        }

        public EventTable Table { get; }
        public Guid CommitId { get; }

        public static Position Execute(DocumentTransaction tx, GetPosition command)
        {
            var sql = $@"
                SELECT ISNULL(MIN(Position), -1) as [begin], ISNULL(MAX(Position), -1) AS [end] 
                FROM {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}
                WHERE CommitId = @commitId";

            return tx.SqlConnection.QuerySingleOrDefault<Position>(sql, new { command.CommitId }, tx.SqlTransaction) ?? new Position(-1L, -1L);
        }
    }
}