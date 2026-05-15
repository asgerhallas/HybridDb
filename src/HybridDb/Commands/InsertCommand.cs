using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Commands
{
    public class InsertCommand : HybridDbCommand<Guid>
    {
        public InsertCommand(DocumentTable table, string id, object projections)
        {
            Table = table;
            Id = id;
            Projections = ConvertAnonymousToProjections(table, projections);
        }

        public string Id { get; }
        public IDictionary<Column, object> Projections { get; }
        public DocumentTable Table { get; }

        public static Guid Execute(DocumentTransaction tx, InsertCommand command)
        {
            var projections = command.Projections.ToDictionary();

            projections[DocumentTable.IdColumn] = command.Id;
            projections[DocumentTable.EtagColumn] = tx.CommitId;
            projections[DocumentTable.CreatedAtColumn] = DateTimeOffset.Now;
            projections[DocumentTable.ModifiedAtColumn] = DateTimeOffset.Now;
            projections[DocumentTable.LastOperationColumn] = Operation.Inserted;

            var columnList = Sql.Join(", ", projections.Keys.Select(col => Sql.From($"{col}")));
            var valueList = Sql.Join(", ", projections.Select(x => Sql.Empty.Append(x.Value, x.Key)));

            var sqlString = Sql.From($"insert into {command.Table} ({columnList}) values ({valueList});")
                .Build(tx.Store, out var parameters);

            DocumentWriteCommand.Execute(tx, new SqlDatabaseCommand
            {
                Sql = sqlString,
                Parameters = parameters,
                ExpectedRowCount = 1
            });

            return tx.CommitId;
        }
    }
}