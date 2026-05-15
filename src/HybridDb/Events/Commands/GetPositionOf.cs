using System;
using Dapper;
using HybridDb.Commands;
using HybridDb.SqlBuilder;

namespace HybridDb.Events.Commands
{
    public class GetPositionOf : HybridDbCommand<Position>
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
            var sql = Sql.From($@"
                SELECT ISNULL(MIN(Position), -1) as [begin], ISNULL(MAX(Position), -1) AS [end] 
                FROM {command.Table}
                WHERE CommitId = @CommitId").Build(tx.Store, out _);

            return tx.SqlConnection.QuerySingleOrDefault<Position>(sql, new { command.CommitId }, tx.SqlTransaction) ?? new Position(-1L, -1L);
        }
    }
}