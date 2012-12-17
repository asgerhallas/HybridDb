using System;
using System.Linq;

namespace HybridDb
{
    public class InsertCommand : DatabaseCommand
    {
        readonly byte[] document;
        readonly Guid key;
        readonly object projections;
        readonly ITable table;

        public InsertCommand(ITable table, Guid key, byte[] document, object projections)
        {
            this.table = table;
            this.key = key;
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
            values.Add(table.IdColumn, key);
            values.Add(table.DocumentColumn, document);

            var sql = string.Format("insert into {0} ({1}) values ({2})",
                                    store.GetFormattedTableName(table),
                                    string.Join(", ", from column in values.Keys select column.Name),
                                    string.Join(", ", from column in values.Keys select "@" + column.Name + uniqueParameterIdentifier));

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }
}