using System;
using System.Linq;

namespace HybridDb
{
    public class UpdateCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly byte[] document;
        readonly Guid key;
        readonly object projections;
        readonly ITable table;

        public UpdateCommand(ITable table, Guid key, Guid etag, byte[] document, object projections)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
            this.document = document;
            this.projections = projections;
        }

        public byte[] Document
        {
            get { return document; }
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values.Add(table.EtagColumn, etag);
            values.Add(table.DocumentColumn, document);

            var sql = string.Format("update {0} set {1} where {2}=@Id{4} and {3}=@CurrentEtag{4}",
                                    store.GetFormattedTableName(table),
                                    string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                                    table.IdColumn.Name,
                                    table.EtagColumn.Name,
                                    uniqueParameterIdentifier);

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);
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