using System.Collections.Generic;

namespace HybridDb
{
    public class SqlBuilder
    {
        readonly List<string> strings;

        public SqlBuilder()
        {
            strings = new List<string>();
        }

        public SqlBuilder Append(string str, params object[] args)
        {
            strings.Add(string.Format(str, args));
            return this;
        }

        public SqlBuilder Append(bool predicate, string str, params object[] args)
        {
            if (predicate) Append(str, args);
            return this;
        }

        public override string ToString()
        {
            return string.Join(" ", strings.ToArray());
        }
    }
}