using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class InsertCommand : DatabaseCommand
    {
        readonly Guid key;
        readonly object projections;
        readonly DocumentTable table;

        public InsertCommand(DocumentTable table, Guid key, object projections)
        {
            this.table = table;
            this.key = key;
            this.projections = projections;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values[table.IdColumn] = key;
            values[table.EtagColumn] = etag;
            values[table.CreatedAtColumn] = DateTimeOffset.Now;
            values[table.ModifiedAtColumn] = DateTimeOffset.Now;

            var sql = string.Format("insert into {0} ({1}) values ({2});",
                store.Database.FormatTableNameAndEscape(table.Name),
                string.Join(", ", from column in values.Keys select column.Name),
                string.Join(", ", from column in values.Keys select "@" + column.Name + uniqueParameterIdentifier));

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }
    }
}