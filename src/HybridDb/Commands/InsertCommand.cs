using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class InsertCommand : Command<Guid>
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
            var table = tx.Store.GetTableFor(command.Table);

            var projections = command.Projections.ToDictionary();

            projections[DocumentTable.IdColumn] = command.Id;
            projections[DocumentTable.EtagColumn] = tx.CommitId;
            projections[DocumentTable.CreatedAtColumn] = DateTimeOffset.Now;
            projections[DocumentTable.ModifiedAtColumn] = DateTimeOffset.Now;
            projections[DocumentTable.LastOperationColumn] = Operation.Inserted;

            var sql = $@"
                insert into {table} 
                ({string.Join(", ", from column in projections.Keys select tx.Store.Database.Escape(column.Name))}) 
                values ({string.Join(", ", from column in projections.Keys select "@" + column.Name)});";

            var parameters = projections.ToHybridDbParameters();

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