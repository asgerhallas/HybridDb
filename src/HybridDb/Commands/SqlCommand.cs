using System;
using HybridDb.SqlBuilder;

namespace HybridDb.Commands
{
    public class SqlCommand : HybridDbCommand<Guid>
    {
        public SqlCommand(Sql sql, int expectedRowCount)
        {
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            ExpectedRowCount = expectedRowCount;
        }

        public Sql Sql { get; }
        public int ExpectedRowCount { get; }

        public static Guid Execute(DocumentTransaction tx, SqlCommand command)
        {
            var sqlString = command.Sql.Build(tx.Store, out var parameters);

            DocumentWriteCommand.Execute(
                tx,
                new SqlDatabaseCommand
                {
                    Sql = sqlString,
                    Parameters = parameters,
                    ExpectedRowCount = command.ExpectedRowCount
                });

            return tx.CommitId;
        }
    }
}