using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : HybridDbCommand<Guid>
    {
        public DocumentTable Table { get; }
        public string Id { get; }
        public Guid? ExpectedEtag { get; }
        public IDictionary<Column, object> Projections { get; }
        public bool LastWriteWins { get; }

        public UpdateCommand(DocumentTable table, string id, Guid? etag, object projections)
        {
            Table = table;
            Id = id;
            ExpectedEtag = etag;
            LastWriteWins = etag == null;
            Projections = ConvertAnonymousToProjections(table, projections);
        }

        public static Guid Execute(DocumentTransaction tx, UpdateCommand command)
        {
            var projections = command.Projections.ToDictionary();

            projections[DocumentTable.EtagColumn] = tx.CommitId;
            projections[DocumentTable.ModifiedAtColumn] = DateTimeOffset.Now;
            projections[DocumentTable.LastOperationColumn] = Operation.Updated;

            var sql = new SqlBuilder()
                .Append($"update {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {string.Join(", ", from column in projections.Keys select $"[{column.Name}] = @{column.Name}")}")
                .Append($"where {DocumentTable.IdColumn.Name}=@Id")
                .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn.Name}=@ExpectedEtag")
                .ToString();

            var parameters = projections.ToHybridDbParameters();

            parameters.Add("@Id", command.Id, DocumentTable.IdColumn);

            if (!command.LastWriteWins)
            {
                parameters.Add("@ExpectedEtag", command.ExpectedEtag, DocumentTable.EtagColumn);
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