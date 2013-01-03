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

        protected static IDictionary<IColumn, object> ConvertAnonymousToProjections(ITable table, object projections)
        {
            return (projections as IDictionary<IColumn, object> ??
                    (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                        .ToDictionary(x => table[x.Key], x => x.Value));
        }

        protected static List<Parameter> MapProjectionsToParameters(IDictionary<IColumn, object> projections, int i)
        {
            return (from projection in projections
                    let column = projection.Key
                    select new Parameter
                    {
                        Name = "@" + column.Name + i, 
                        Value = projection.Value, 
                        DbType = column.Column.DbType, 
                        Size = column.Column.Length
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