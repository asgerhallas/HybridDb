using System;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class DeleteCommand : Command<Guid>
    {
        public DocumentTable Table { get; }
        public string Key { get; }
        public Guid ExpectedEtag { get; }
        public bool LastWriteWins { get; }

        public DeleteCommand(DocumentTable table, string key, Guid etag, bool lastWriteWins)
        {
            Table = table;
            Key = key;
            ExpectedEtag = etag;
            LastWriteWins = lastWriteWins;
        }

        public static Guid Execute(DocumentTransaction tx, DeleteCommand command)
        {
            // Note that last write wins can actually still produce a ConcurrencyException if the 
            // row was already deleted, which would result in 0 resulting rows changed

            var sql = new SqlBuilder()
                .Append($"update {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {DocumentTable.IdColumn.Name} = @NewId")
                .Append($", {DocumentTable.LastOperationColumn.Name} = {(byte)Operation.Deleted}")
                .Append($"where {DocumentTable.IdColumn.Name} = @Id")
                .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn.Name} = @ExpectedEtag")
                .ToString();

            var parameters = new Parameters();
            parameters.Add("@Id", command.Key, SqlTypeMap.Convert(DocumentTable.IdColumn).DbType, null);
            parameters.Add("@NewId", $"{command.Key}/{Guid.NewGuid()}", SqlTypeMap.Convert(DocumentTable.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                parameters.Add("@ExpectedEtag", command.ExpectedEtag, SqlTypeMap.Convert(DocumentTable.EtagColumn).DbType, null);
            }

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            });

            return tx.CommitId;
        }
    }
}