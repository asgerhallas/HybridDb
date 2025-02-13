using System;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class DeleteCommand : Command<Guid>
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

            var sql = new SqlBuilderOld();
            var parameters = new HybridDbParameters();

            if (tx.Store.Configuration.SoftDelete)
            {
                sql
                    .Append($"update {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                    .Append($"set {DocumentTable.IdColumn.Name} = @NewId")
                    .Append($", {DocumentTable.LastOperationColumn.Name} = {(byte) Operation.Deleted}")
                    .Append($"where {DocumentTable.IdColumn.Name} = @Id")
                    .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn.Name} = @ExpectedEtag");

                parameters.Add("@NewId", $"{command.Key}/{Guid.NewGuid()}", DocumentTable.IdColumn);
            }
            else
            {
                sql
                    .Append($"delete from {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                    .Append($"where {DocumentTable.IdColumn.Name} = @Id")
                    .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn.Name} = @ExpectedEtag");
            }

            parameters.Add("@Id", command.Key, DocumentTable.IdColumn);

            if (!command.LastWriteWins)
            {
                parameters.Add("@ExpectedEtag", command.ExpectedEtag, DocumentTable.EtagColumn);
            }

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sql.ToString(),
                Parameters = parameters,
                ExpectedRowCount = 1
            });

            return tx.CommitId;
        }
    }
}