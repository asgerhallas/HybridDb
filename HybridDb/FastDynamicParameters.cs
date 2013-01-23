using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using System.Linq;

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
                if (parameter.DbType.HasValue)
                    dbDataParameter.DbType = parameter.DbType.Value;
                dbDataParameter.Size = parameter.Size ?? 0;
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