using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.SqlBuilder;

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

            var sql = Sql.Empty
                .Append($"update {command.Table}")
                .Append("set", Sql.Join(", ", projections.Select(x => Sql.From($"{x.Key} = {x.Value}"))))
                .Append($"where {DocumentTable.IdColumn} = {command.Id}")
                .Append(!command.LastWriteWins, $"and {DocumentTable.EtagColumn} = {command.ExpectedEtag}");

            var sqlString = sql.Build(tx.Store, out var parameters);

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sqlString,
                Parameters = parameters,
                ExpectedRowCount = 1,
                Table = command.Table,
                DocumentId = command.Id
            });

            return tx.CommitId;
        }
    }
}