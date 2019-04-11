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

            values[command.Table.IdColumn] = command.Id;
            values[command.Table.EtagColumn] = tx.CommitId;
            values[command.Table.CreatedAtColumn] = DateTimeOffset.Now;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;
            values[command.Table.LastOperationColumn] = Operation.Inserted;

            var sql = $@"
                insert into {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} 
                ({string.Join(", ", from column in values.Keys select column.Name)}) 
                values ({string.Join(", ", from column in values.Keys select "@" + column.Name)});";

            var parameters = MapProjectionsToParameters(values);

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