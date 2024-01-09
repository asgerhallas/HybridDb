using System;

namespace HybridDb.Commands
{
    public class SqlCommand : Command<Guid>
    {
        public SqlCommand(SqlBuilder sql, int expectedRowCount)
        {
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            ExpectedRowCount = expectedRowCount;
        }

        public SqlBuilder Sql { get; }
        public int ExpectedRowCount { get; }

        public static Guid Execute(DocumentTransaction tx, SqlCommand command)
        {
            DocumentWriteCommand.Execute(
                tx,
                new SqlDatabaseCommand
                {
                    Sql = command.Sql.ToString(),
                    Parameters = command.Sql.Parameters,
                    ExpectedRowCount = command.ExpectedRowCount
                });

            return tx.CommitId;
        }
    }
}