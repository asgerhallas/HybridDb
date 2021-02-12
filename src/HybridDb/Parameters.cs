using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HybridDb.Config;

namespace HybridDb
{
    public class Parameters : SqlMapper.IDynamicParameters
    {
        readonly List<SqlParameter> parameters;

        public Parameters(params SqlParameter[] parameters) => this.parameters = parameters.ToList();
        public Parameters(IEnumerable<SqlParameter> parameters) => this.parameters = parameters.ToList();

        public int Count => parameters.Count;

        public static Parameters FromAnonymousObject(object parameters)
        {
            if (parameters is IEnumerable<SqlParameter> enumerable) return new Parameters(enumerable);

            return new Parameters(
                from projection in parameters as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(parameters)
                select CreateSqlParameter(projection.Key, projection.Value, null));
        }

        public static Parameters FromProjections(IDictionary<Column, object> projections) =>
            new Parameters(projections.Select(projection =>
            {
                var column = projection.Key;
                var sqlColumn = SqlTypeMap.Convert(column);

                return CreateSqlParameter($"@{column.Name}", projection.Value, sqlColumn.DbType);
            }));

        public void Add(string name, object value, SqlDbType? dbType, string size) => parameters.Add(CreateSqlParameter(name, value, dbType));
        public void Add(Parameters moreParameters) => parameters.AddRange(moreParameters.parameters);

        public static SqlParameter CreateSqlParameter(string name, object value, SqlDbType? dbType)
        {
            // Size is set implicitly by setting Value, see https://msdn.microsoft.com/en-us/library/system.data.common.dbparameter.size(v=vs.110).aspx
            var parameter = new SqlParameter(Clean(name), value ?? DBNull.Value)
            {
                Direction = ParameterDirection.Input
            };

            //// DbType is inferred too, but can be overriden for some columns
            if (dbType != null)
            {
                parameter.SqlDbType = dbType.Value;
            }

            return parameter;
        }

        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            if (!(command is SqlCommand)) throw new ArgumentException("HybridDb only supports Sql Server.");

            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }

        static string Clean(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                switch (name[0])
                {
                    case '@':
                    case ':':
                    case '?':
                        return name.Substring(1);
                }
            }

            return name;
        }
    }
}