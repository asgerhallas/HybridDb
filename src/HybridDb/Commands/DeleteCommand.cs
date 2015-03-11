using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class DeleteCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly bool lastWriteWins;
        readonly Guid key;
        readonly DocumentTable table;

        public DeleteCommand(DocumentTable table, Guid key, Guid etag, bool lastWriteWins)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
            this.lastWriteWins = lastWriteWins;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var sql = new SqlBuilder()
                .Append("delete from {0} where {1} = @Id{2}",
                        store.Database.FormatTableNameAndEscape(table.Name),
                        table.IdColumn.Name,
                        uniqueParameterIdentifier)
                .Append(!lastWriteWins,
                        "and {0} = @CurrentEtag{1}",
                        table.EtagColumn.Name,
                        uniqueParameterIdentifier)
                .ToString();

            var parameters = new Dictionary<string, Parameter>();
            AddTo(parameters, "@Id" + uniqueParameterIdentifier, key, SqlTypeMap.GetDbType(table.IdColumn.Type), null);

            if (!lastWriteWins)
            {
                AddTo(parameters, "@CurrentEtag" + uniqueParameterIdentifier, currentEtag, SqlTypeMap.GetDbType(table.EtagColumn.Type), null);
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