using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class UpdateCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly Guid key;
        readonly object projections;
        readonly bool lastWriteWins;
        readonly DocumentTable table;

        public UpdateCommand(DocumentTable table, Guid key, Guid etag, object projections, bool lastWriteWins)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
            this.projections = projections;
            this.lastWriteWins = lastWriteWins;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values[table.EtagColumn] = etag;
            values[table.ModifiedAtColumn] = DateTimeOffset.Now;

            var sql = new SqlBuilder()
                .Append("update {0} set {1} where {2}=@Id{3}",
                        store.Database.FormatTableNameAndEscape(table.Name),
                        string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                        table.IdColumn.Name,
                        uniqueParameterIdentifier)
                .Append(!lastWriteWins, "and {0}=@CurrentEtag{1}",
                        table.EtagColumn.Name,
                        uniqueParameterIdentifier)
                .ToString();

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);
            AddTo(parameters, "@Id" + uniqueParameterIdentifier, key, table.IdColumn.SqlColumn.Type, null);

            if (!lastWriteWins)
            {
                AddTo(parameters, "@CurrentEtag" + uniqueParameterIdentifier, currentEtag, table.EtagColumn.SqlColumn.Type, null);
            }

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }
    }
}