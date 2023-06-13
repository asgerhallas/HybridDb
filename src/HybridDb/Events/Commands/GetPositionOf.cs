using System;
using Dapper;
using HybridDb.Commands;

namespace HybridDb.Events.Commands
{
    public class GetPositionOf : Command<Position>
    {
        public GetPositionOf(EventTable table, Guid commitId)
        {
            Table = table;
            CommitId = commitId;
        }

        public EventTable Table { get; }
        public Guid CommitId { get; }

        public static Position Execute(DocumentTransaction tx, GetPositionOf command)
        {
            var table = command.Table.GetSpicy(tx.Store);

            var sql = $@"
                SELECT ISNULL(MIN(Position), -1) as [begin], ISNULL(MAX(Position), -1) AS [end] 
                FROM {table}
                WHERE CommitId = @CommitId";

            return tx.SqlConnection.QuerySingleOrDefault<Position>(sql, new { command.CommitId }, tx.SqlTransaction) ?? new Position(-1L, -1L);
        }
    }
}