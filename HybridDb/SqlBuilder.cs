using System;
using System.Collections.Generic;

namespace HybridDb
{
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

        public override string ToString()
        {
            return string.Join(" ", strings.ToArray());
        }
    }
}