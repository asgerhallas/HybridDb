using System;
using Dapper;

namespace HybridDb
{
    public class DeleteCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly Guid key;
        readonly ITable table;

        public DeleteCommand(ITable table, Guid key, Guid etag)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var sql = string.Format("delete from {0} where {1} = @Id{3} and {2} = @CurrentEtag{3}",
                                    store.GetFormattedTableName(table),
                                    table.IdColumn.Name,
                                    table.EtagColumn.Name,
                                    uniqueParameterIdentifier);

            var parameters = new DynamicParameters();
            parameters.Add("@Id" + uniqueParameterIdentifier, key, table.IdColumn.Column.DbType);
            parameters.Add("@CurrentEtag" + uniqueParameterIdentifier, currentEtag, table.EtagColumn.Column.DbType);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }
}