using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using HybridDb.Schema;

namespace HybridDb.Commands
{
    public abstract class DatabaseCommand
    {
        internal abstract PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier);

        protected static IDictionary<Column, object> ConvertAnonymousToProjections(Table table, object projections)
        {
            return (projections as IDictionary<Column, object> ??
                    (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                        .ToDictionary(x => table.GetColumnOrDefaultColumn(x.Key, x.Value.GetTypeOrDefault()), x => x.Value));
        }

        protected static Dictionary<string, Parameter> MapProjectionsToParameters(IDictionary<Column, object> projections, int i)
        {
            var parameters = new Dictionary<string, Parameter>();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                AddTo(parameters, "@" + column.Name + i, projection.Value, column.SqlColumn.Type, column.SqlColumn.Length);
            }

            return parameters;
        }

        public static void AddTo(Dictionary<string, Parameter> parameters, string name, object value, DbType? dbType, int? size)
        {
            parameters[name] = new Parameter {Name = name, Value = value, DbType = dbType, Size = size};
        }

        public class PreparedDatabaseCommand
        {
            public string Sql { get; set; }
            public List<Parameter> Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }
    }
}