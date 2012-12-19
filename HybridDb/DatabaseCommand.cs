using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace HybridDb
{
    public abstract class DatabaseCommand
    {
        internal abstract PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier);

        protected static IDictionary<IColumn, object> ConvertAnonymousToProjections(ITable table, object projections)
        {
            return (projections as IDictionary<IColumn, object> ??
                    (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                        .ToDictionary(x => table[x.Key], x => x.Value));
        }

        protected static DynamicParameters MapProjectionsToParameters(IDictionary<IColumn, object> projections, int i)
        {
            var parameters = new DynamicParameters();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                parameters.Add("@" + column.Name + i,
                               projection.Value,
                               column.Column.DbType,
                               size: column.Column.Length);
            }

            return parameters;
        }

        public class PreparedDatabaseCommand
        {
            public string Sql { get; set; }
            public DynamicParameters Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }
    }
}