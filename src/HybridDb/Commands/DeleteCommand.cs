using System;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Commands
{
    public class DeleteCommand : HybridDbCommand<Guid>
    {
        public DocumentTable Table { get; }
        public string Key { get; }
        public Guid? ExpectedEtag { get; }
        public bool LastWriteWins { get; }

        public DeleteCommand(DocumentTable table, string key, Guid? etag)
        {
            Table = table;
            Key = key;
            ExpectedEtag = etag;
            LastWriteWins = etag == null;
        }

        public static Guid Execute(DocumentTransaction tx, DeleteCommand command)
        {
            // Note that last write wins can actually still produce a ConcurrencyException if the 
            // row was already deleted, which would result in 0 resulting rows changed

            var sql = Sql.Empty;

            if (tx.Store.Configuration.SoftDelete)
            {
                sql
                    .Append($"update {command.Table}")
                    .Append($"set {DocumentTable.IdColumn} = {$"{command.Key}/{Guid.NewGuid()}"}")
                    .Append($", {DocumentTable.LastOperationColumn} = {(byte)Operation.Deleted}")
                    .Append($"where {DocumentTable.IdColumn} = {command.Key}")
                    .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn} = {command.ExpectedEtag}");
            }
            else
            {
                sql
                    .Append($"delete from {command.Table}")
                    .Append($"where {DocumentTable.IdColumn} = {command.Key}")
                    .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn} = {command.ExpectedEtag}");
            }

            var sqlString = sql.Build(tx.Store, out var parameters);

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sqlString,
                Parameters = parameters,
                ExpectedRowCount = 1,
                Table = command.Table,
                DocumentId = command.Key
            });

            return tx.CommitId;
        }
    }
}