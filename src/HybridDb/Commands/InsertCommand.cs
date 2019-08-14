using System;
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
            Projections = projections;
        }

        public string Id { get; }
        public object Projections { get; }
        public DocumentTable Table { get; }

        public static Guid Execute(DocumentTransaction tx, InsertCommand command)
        {
            var values = ConvertAnonymousToProjections(command.Table, command.Projections);

            values[DocumentTable.IdColumn] = command.Id;
            values[DocumentTable.EtagColumn] = tx.CommitId;
            values[DocumentTable.CreatedAtColumn] = DateTimeOffset.Now;
            values[DocumentTable.ModifiedAtColumn] = DateTimeOffset.Now;
            values[DocumentTable.LastOperationColumn] = Operation.Inserted;

            var sql = $@"
                insert into {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} 
                ({string.Join(", ", from column in values.Keys select column.Name)}) 
                values ({string.Join(", ", from column in values.Keys select "@" + column.Name)});";

            var parameters = Parameters.FromProjections(values);

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