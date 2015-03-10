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

            //var collectionProjections = values.Where(x => x.Key is CollectionColumn)
            //                                  .ToDictionary(x => (CollectionColumn) x.Key, x => x.Value);

            //foreach (var collectionProjection in collectionProjections)
            //{
            //    var projectionTable = collectionProjection.Key.Table;

            //    var blahs = new Dictionary<Column, object> { { collectionProjection.Key, collectionProjection.Value } };

            //    blahs.Add(projectionTable.DocumentIdColumn, key);
            //    //blahs.Add(projectionTable.DocumentColumn, document);

            //    //sql += string.Format("insert into {0} ({1}) values ({2});",
            //    //                     store.Escape(store.GetFormattedTableName(projectionTable)),
            //    //                     string.Join(", ", from column in blahs.Keys select column.Name),
            //    //                     string.Join(", ", from column in blahs.Keys select "@" + column.Name + uniqueParameterIdentifier));
            //}

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