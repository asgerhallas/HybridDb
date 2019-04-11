using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public abstract class Command
    {
        public static IDictionary<Column, object> ConvertAnonymousToProjections(Table table, object projections) =>
            projections as IDictionary<Column, object> ?? (
                from projection in projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections)
                let column = table[projection.Key]
                where column != null
                select new KeyValuePair<Column, object>(column, projection.Value)
            ).ToDictionary();

        public static Dictionary<string, Parameter> MapProjectionsToParameters(IDictionary<Column, object> projections)
        {
            var parameters = new Dictionary<string, Parameter>();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                var sqlColumn = SqlTypeMap.Convert(column);
                AddTo(parameters, "@" + column.Name, projection.Value, sqlColumn.DbType, sqlColumn.Length);
            }

            return parameters;
        }

        public static void AddTo(IDictionary<string, Parameter> parameters, string name, object value, SqlDbType? dbType, string size)
        {
            parameters[name] = new Parameter { Name = name, Value = value, DbType = dbType };
        }
    }

    public abstract class Command<TResult> : Command { }
}