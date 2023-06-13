using System;
using System.Linq;
using Dapper;

namespace HybridDb.Events.Commands
{
    public class LoadParentCommit : Command<Commit<byte[]>>
    {
        public LoadParentCommit(EventTable table, Guid? commitId)
        {
            Table = table;
            CommitId = commitId;
        }

        public EventTable Table { get; }
        public Guid? CommitId { get; }

        public static Commit<byte[]> Execute(DocumentTransaction tx, LoadParentCommit command)
        {
            if (command.CommitId == Guid.Empty) return null;

            var table = command.Table.GetSpicy(tx.Store);

            var sql =
                $@"SELECT
                    EventId,
                    Position,
                    CommitId,
                    StreamId,
                    SequenceNumber,
                    Name,
                    Generation,
                    Data, 
                    Metadata
                  FROM {table}
                  WHERE CommitId = (
                    SELECT TOP 1 CommitId
                    FROM {table}
                    WHERE Position < ISNULL(
                        (SELECT MIN(Position)
                        FROM {table} 
                        WHERE CommitId = @id),
                        (SELECT MAX(Position) + 1
                        FROM {table})
                    )
                    ORDER BY Position DESC
                  )
                  ORDER BY Position";

            var rows = tx.SqlConnection.Query<Row>(sql, new {id = command.CommitId}, transaction: tx.SqlTransaction).ToList();

            // if no parent commit is found, the initial, transient commit is parent
            if (!rows.Any())
            {
                return new Commit<byte[]>(Guid.Empty, 0, -1, -1);
            }

            var lastRow = rows.Last();

            return Commit.Create(lastRow.CommitId, lastRow.Generation, lastRow.Position, rows.Select(Row.ToEvent).ToList());
        }
    }
}