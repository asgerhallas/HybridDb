using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : Command<Guid>
    {
        public DocumentTable Table { get; }
        public string Id { get; }
        public Guid? ExpectedEtag { get; }
        public object Projections { get; }
        public bool LastWriteWins { get; }

        public UpdateCommand(DocumentTable table, string id, Guid? etag, object projections)
        {
            Table = table;
            Id = id;
            ExpectedEtag = etag;
            LastWriteWins = etag == null;
            Projections = projections;
        }

        public static Guid Execute(DocumentTransaction tx, UpdateCommand command)
        {
            var values = ConvertAnonymousToProjections(command.Table, command.Projections); 

            values[DocumentTable.EtagColumn] = tx.CommitId;
            values[DocumentTable.ModifiedAtColumn] = DateTimeOffset.Now;
            values[DocumentTable.LastOperationColumn] = Operation.Updated;

            var sql = new SqlBuilder()
                .Append($"update {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {string.Join(", ", from column in values.Keys select column.Name + " = @" + column.Name)}")
                .Append($"where {DocumentTable.IdColumn.Name}=@Id")
                .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn.Name}=@ExpectedEtag")
                .ToString();

            var parameters = Parameters.FromProjections(values);

            parameters.Add("@Id", command.Id, SqlTypeMap.Convert(DocumentTable.IdColumn).DbType, null);

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