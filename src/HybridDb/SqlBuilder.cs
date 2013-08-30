using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public class Sql
    {
        readonly string sql;
        readonly List<Parameter> parameters;

        public Sql(string sql, object parameters)
        {
            this.sql = "";
            this.parameters = new List<Parameter>();
        }

        public IEnumerable<Parameter> Parameters
        {
            get { return parameters; }
        }

        public static implicit operator Sql(string s)
        {
            return new Sql(s, null);
        }

        public override string ToString()
        {
            return sql;
        }
    }

    public class SqlBuilder
    {
        readonly List<string> strings;
        readonly List<Parameter> parameters;

        bool? previousPredicate;

        public SqlBuilder()
        {
            strings = new List<string>();
            parameters = new List<Parameter>();
            previousPredicate = null;
        }

        public IEnumerable<Parameter> Parameters
        {
            get { return parameters; }
        }

        public SqlBuilder Append(string sql, params object[] args)
        {
            var formatArgs = new List<object>();

            foreach (var arg in args)
            {
                var parameter = arg as Parameter;
                if (parameter != null)
                {
                    parameters.Add(parameter);
                }
                else
                {
                    formatArgs.Add(arg);
                }
            }

            strings.Add(string.Format(sql, formatArgs.ToArray()));
            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, params object[] args)
        {
            previousPredicate = predicate;
            if (predicate) Append(sql, args);
            return this;
        }

        public SqlBuilder Or(string sql, params object[] args)
        {
            if (!previousPredicate.HasValue)
                throw new InvalidOperationException("Cannot use Or() when no Append with condition has been run.");

            if (!previousPredicate.Value) Append(sql, args);

            previousPredicate = null;
            return this;
        }

        public SqlBuilder Append(SqlBuilder builder)
        {
            strings.AddRange(builder.strings);
            parameters.AddRange(builder.parameters);
            return this;
        }

        public override string ToString()
        {
            return string.Join(" ", strings.ToArray());
        }

        public string ToDynamicSql()
        {
            var sql = "declare @sql nvarchar(4000) = " +
                      string.Join(" + ", strings.Select(x => "' " + x + " '").ToArray()) +
                      "; exec(@sql);";

            sql = parameters
                .OrderByDescending(x => x.Name.Length)
                .Select(parameter => "@" + parameter.Name)
                .Aggregate(sql, (current, sqlName) => current.Replace(sqlName, "' + quotename(" + sqlName + ", '''') + '"));

            return sql;
        }
    }
}