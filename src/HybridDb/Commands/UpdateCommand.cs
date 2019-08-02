using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : Command<Guid>
    {
        public DocumentTable Table { get; }
        public string Id { get; }
        public Guid ExpectedEtag { get; }
        public object Projections { get; }
        public bool LastWriteWins { get; }

        public UpdateCommand(DocumentTable table, string id, Guid etag, object projections, bool lastWriteWins)
        {
            Table = table;
            Id = id;
            ExpectedEtag = etag;
            Projections = projections;
            LastWriteWins = lastWriteWins;
        }

        public static Guid Execute(DocumentTransaction tx, UpdateCommand command)
        {
            var values = ConvertAnonymousToProjections(command.Table, command.Projections); 

            values[command.Table.EtagColumn] = tx.CommitId;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;
            values[command.Table.LastOperationColumn] = Operation.Updated;

            var sql = new SqlBuilder()
                .Append($"update {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {string.Join(", ", from column in values.Keys select column.Name + " = @" + column.Name)}")
                .Append($"where {command.Table.IdColumn.Name}=@Id")
                .Append(!command.LastWriteWins, $"and {command.Table.EtagColumn.Name}=@ExpectedEtag")
                .ToString();

            var parameters = MapProjectionsToParameters(values);

            AddTo(parameters, "@Id", command.Id, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

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