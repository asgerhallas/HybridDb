using System;
using System.Linq;
using HybridDb.Schema;

namespace HybridDb.Commands
{
    public class InsertCommand : DatabaseCommand
    {
        readonly Guid key;
        readonly object projections;
        readonly Table table;

        public InsertCommand(Table table, Guid key, object projections)
        {
            this.table = table;
            this.key = key;
            this.projections = projections;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            var simpleProjections = (from value in values where !(value.Key is SystemColumn) select value).ToDictionary();
            simpleProjections.Add(table.IdColumn, key);
            simpleProjections.Add(table.EtagColumn, etag);
            simpleProjections.Add(table.CreatedAtColumn, DateTimeOffset.Now);
            simpleProjections.Add(table.ModifiedAtColumn, DateTimeOffset.Now);

            var sql = string.Format("insert into {0} ({1}) values ({2});",
                                    store.FormatTableNameAndEscape(table.Name),
                                    string.Join(", ", from column in simpleProjections.Keys select column.Name),
                                    string.Join(", ", from column in simpleProjections.Keys select "@" + column.Name + uniqueParameterIdentifier));

            var collectionProjections = values.Where(x => x.Key is CollectionColumn)
                                              .ToDictionary(x => (CollectionColumn) x.Key, x => x.Value);

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

            var parameters = MapProjectionsToParameters(simpleProjections, uniqueParameterIdentifier);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }
    }
}