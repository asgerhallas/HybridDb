using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace HybridDb
{
    public class SqlBuilder
    {
        const string signs = "abcdefghijklmnopqrstuvxyz";

        readonly string uniquePrefix;
        readonly HashSet<string> usedUniquePrefixes = new();

        readonly StringBuilder fragments;
        public readonly HybridDbParameters parameters;

        public SqlBuilder()
        {
            var signsLength = signs.Length;

            uniquePrefix = new string(
                Guid.NewGuid().ToByteArray()
                    .Take(8)
                    .Select(x => signs[x % signsLength])
                    .ToArray());

            usedUniquePrefixes.Add(uniquePrefix);

            fragments = new StringBuilder();
            parameters = new HybridDbParameters();
        }

        public HybridDbParameters Parameters => parameters;

        public SqlBuilder Append(string sql, params SqlParameter[] args)
        {
            foreach (var arg in args)
            {
                var oldParameterName = HybridDbParameters.Clean(arg.ParameterName);
                var newParameterName = $"{uniquePrefix}_{oldParameterName}";
                
                var newSql = sql.Replace($"@{oldParameterName}", $"@{newParameterName}");

                if (newSql != sql)
                {
                    sql = newSql;
                    arg.ParameterName = newParameterName;
                }

                parameters.Add(arg);
            }

            if (fragments.Length != 0) fragments.Append(" ");

            fragments.Append(sql);

            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, params SqlParameter[] args)
        {
            if (predicate) Append(sql, args);
            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, string orSql, params SqlParameter[] args) => 
            predicate ? Append(sql, args) : Append(orSql, args);

        public SqlBuilder Append(SqlBuilder builder)
        {
            if (usedUniquePrefixes.Overlaps(builder.usedUniquePrefixes))
                throw new InvalidOperationException("UniquePrefixes are not unique.");

            usedUniquePrefixes.UnionWith(builder.usedUniquePrefixes);
            fragments.Append(builder.fragments);
            parameters.Add(builder.parameters);

            return this;
        }

        public override string ToString() => string.Join(" ", fragments);
    }
}