using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Schema;

namespace HybridDb.Commands
{
    public abstract class DatabaseCommand
    {
        internal abstract PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier);

        protected static IDictionary<Column, object> ConvertAnonymousToProjections(ITable table, object projections)
        {
            return (projections as IDictionary<Column, object> ??
                    (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                        .ToDictionary(x => table.GetNamedOrDynamicColumn(x.Key, x.Value), x => x.Value));
        }

        protected static List<Parameter> MapProjectionsToParameters(IDictionary<Column, object> projections, int i)
        {
            return (from projection in projections
                    let column = projection.Key
                    select new Parameter
                    {
                        Name = "@" + column.Name + i, 
                        Value = projection.Value, 
                        DbType = column.SqlColumn.Type,
                        Size = column.SqlColumn.Length
                    }).ToList();
        }

        public class PreparedDatabaseCommand
        {
            public string Sql { get; set; }
            public List<Parameter> Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }
    }
}