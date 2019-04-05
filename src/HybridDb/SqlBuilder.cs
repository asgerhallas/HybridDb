using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HybridDb
{
    public class SqlBuilder
    {
        readonly StringBuilder fragments;
        readonly List<Parameter> parameters;

        public SqlBuilder()
        {
            fragments = new StringBuilder();
            parameters = new List<Parameter>();
        }

        public IEnumerable<Parameter> Parameters => parameters;

        public SqlBuilder Append(string sql, params Parameter[] args)
        {
            foreach (var arg in args)
            {
                parameters.Add(arg);
            }

            fragments.Append(sql);

            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, params Parameter[] args)
        {
            if (predicate) Append(sql, args);
            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, string orSql, params Parameter[] args) => 
            predicate ? Append(sql, args) : Append(orSql, args);

        public SqlBuilder Append(SqlBuilder builder)
        {
            fragments.Append(builder.fragments);
            parameters.AddRange(builder.parameters);
            return this;
        }

        public override string ToString() => string.Join(" ", fragments);
    }
}