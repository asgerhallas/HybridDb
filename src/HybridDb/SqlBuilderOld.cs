using System.Text;
using Microsoft.Data.SqlClient;

namespace HybridDb
{
    public class SqlBuilderOld
    {
        readonly StringBuilder fragments;

        public SqlBuilderOld(string sql = null, params SqlParameter[] parameters)
        {
            fragments = new StringBuilder();
            Parameters = new HybridDbParameters();
            Append(sql, parameters);
        }

        public HybridDbParameters Parameters { get; }

        public SqlBuilderOld Append(string sql, params SqlParameter[] args)
        {
            if (args != null)
            {
                foreach (var arg in args)
                {
                    Parameters.Add(arg);
                }
            }

            if (sql != null)
            {
                if (fragments.Length != 0) fragments.Append(" ");

                fragments.Append(sql);
            }

            return this;
        }

        public SqlBuilderOld Append(bool predicate, string sql, params SqlParameter[] args)
        {
            if (predicate) Append(sql, args);

            return this;
        }

        public SqlBuilderOld Append(bool predicate, string sql, string orSql, params SqlParameter[] args) =>
            predicate ? Append(sql, args) : Append(orSql, args);

        public SqlBuilderOld Append(SqlBuilderOld builderOld)
        {
            fragments.Append(builderOld.fragments);
            Parameters.Add(builderOld.Parameters);

            return this;
        }

        public override string ToString() => string.Join(" ", fragments);
    }
}