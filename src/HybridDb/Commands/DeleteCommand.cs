using System;
using System.Collections.Generic;
using System.Linq;
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
                .Append($"set {command.Table.IdColumn.Name} = @NewId")
                .Append($", {command.Table.LastOperationColumn.Name} = {(byte)Operation.Deleted}")
                .Append($"where {command.Table.IdColumn.Name} = @Id")
                .Append(!command.LastWriteWins, $"and {command.Table.EtagColumn.Name} = @ExpectedEtag")
                .ToString();

            var parameters = new Dictionary<string, Parameter>();
            AddTo(parameters, "@Id", command.Key, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);
            AddTo(parameters, "@NewId", $"{command.Key}/{Guid.NewGuid()}", SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                AddTo(parameters, "@ExpectedEtag", command.ExpectedEtag, SqlTypeMap.Convert(command.Table.EtagColumn).DbType, null);
            }

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            });

            return tx.CommitId;
        }
    }
}