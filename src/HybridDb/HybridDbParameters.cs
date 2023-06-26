using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HybridDb.Config;
using ShinySwitch;

namespace HybridDb
{
    // This works mostly like the Dapper built-in DynamicParamters, but this only support SqlServer
    // and it also persists enums as strings, which was 
    public class HybridDbParameters : SqlMapper.IDynamicParameters
    {
        readonly List<SqlParameter> parameters = new();

        public int Count => parameters.Count;

        public IReadOnlyList<SqlParameter> Parameters => parameters;

        public void Add(object addition)
        {
            if (addition == null) return;

            Switch.On(addition)
                .Match<HybridDbParameters>(x => parameters.AddRange(x.parameters))
                .Match<SqlParameter>(x => parameters.Add(x))
                .Match<IEnumerable<SqlParameter>>(x =>
                {
                    foreach (var sqlParameter in x)
                    {
                        parameters.Add(sqlParameter);
                    }
                })
                .Match<IDictionary<Column, object>>(x =>
                {
                    foreach (var keyValue in x)
                    {
                        var column = keyValue.Key;
                        var sqlColumn = SqlTypeMap.Convert(column);

                        parameters.Add(CreateSqlParameter($"@{column.Name}", keyValue.Value, sqlColumn.DbType));
                    }
                })
                .Else(x =>
                {
                    foreach (var keyValue in ObjectToDictionaryRegistry.Convert(x))
                    {
                        parameters.Add(CreateSqlParameter(keyValue.Key, keyValue.Value, null));
                    }
                });
        }

        public void Add(string name, object value, Column column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            var sqlColumn = SqlTypeMap.Convert(column);

            Add(name, value, sqlColumn.DbType);

        }

        public void Add(string name, object value, SqlDbType dbType)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            Add(CreateSqlParameter(name, value, dbType));
        }

        public static SqlParameter CreateSqlParameter(string name, object value, SqlDbType? dbType)
        {
            // Size is set implicitly by setting Value, see https://msdn.microsoft.com/en-us/library/system.data.common.dbparameter.size(v=vs.110).aspx
            var parameter = new SqlParameter(Clean(name), value ?? DBNull.Value)
            {
                Direction = ParameterDirection.Input
            };

            // DbType is inferred too, but can be overriden for some columns
            // This is useful for enums, that should be parametized as string 
            if (dbType != null)
            {
                parameter.SqlDbType = dbType.Value;
            }

            return parameter;
        }

        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            if (command is not SqlCommand) throw new ArgumentException("HybridDb only supports SqlServer.");

            foreach (var parameter in parameters)
            {
                if (parameter.Value is IEnumerable<object>)
                {
#pragma warning disable CS0618
                    SqlMapper.PackListParameters(command, parameter.ParameterName, parameter.Value);
#pragma warning restore CS0618

                    continue;
                }

                command.Parameters.Add(parameter);
            }
        }

        static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            return name[0] switch
            {
                '@' => name.Substring(1),
                ':' => name.Substring(1),
                '?' => name.Substring(1),
                _ => name
            };
        }
    }
}