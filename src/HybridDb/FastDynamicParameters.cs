using System;
using System.Collections.Generic;
using System.Data;
using Dapper;

namespace HybridDb
{
    public class FastDynamicParameters : SqlMapper.IDynamicParameters
    {
        readonly IEnumerable<Parameter> parameters;

        public FastDynamicParameters(IEnumerable<Parameter> parameters)
        {
            this.parameters = parameters;
        }

        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            foreach (var parameter in parameters)
            {
                var dbDataParameter = command.CreateParameter();
                dbDataParameter.ParameterName = Clean(parameter.Name);
                dbDataParameter.Value = parameter.Value ?? DBNull.Value;
                dbDataParameter.Direction = ParameterDirection.Input;

                // Size is set implicitly by setting Value, see https://msdn.microsoft.com/en-us/library/system.data.common.dbparameter.size(v=vs.110).aspx
                // DbType is inferred too, but can be overriden for some columns
                if (parameter.DbType.HasValue)
                {
                    dbDataParameter.DbType = parameter.DbType.Value;
                }

                command.Parameters.Add(dbDataParameter);
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