using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Schema;

namespace HybridDb.Commands
{
    public class InsertCommand : DatabaseCommand
    {
        readonly byte[] document;
        readonly Guid key;
        readonly object projections;
        readonly Table table;

        public InsertCommand(Table table, Guid key, byte[] document, object projections)
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

            var simpleProjections = (from value in values where value.Key is UserColumn select value).ToDictionary();
            simpleProjections.Add(table.EtagColumn, etag);
            simpleProjections.Add(table.IdColumn, key);
            simpleProjections.Add(table.DocumentColumn, document);

            var sql = string.Format("insert into {0} ({1}) values ({2});",
                                    store.Escape(table.GetFormattedName(store.TableMode)),
                                    string.Join(", ", from column in simpleProjections.Keys select column.Name),
                                    string.Join(", ", from column in simpleProjections.Keys select "@" + column.Name + uniqueParameterIdentifier));


            //var collectionProjections = values.Where(x => x.Key is CollectionProjectionColumn)
            //                                  .ToDictionary(x => (CollectionProjectionColumn) x.Key, x => x.Value);

            //foreach (var collectionProjection in collectionProjections)
            //{
            //    var projectionTable = collectionProjection.Key.Table;

            //    var blahs = new Dictionary<Column, object> {{collectionProjection.Key, collectionProjection.Value}};

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
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }
}