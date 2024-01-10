using System.Text;
using Microsoft.Data.SqlClient;

namespace HybridDb
{
    public class SqlBuilder
    {
        readonly StringBuilder fragments;

        public SqlBuilder(string sql = null, params SqlParameter[] parameters)
        {
            fragments = new StringBuilder();
            Parameters = new HybridDbParameters();
            Append(sql, parameters);
        }

        public HybridDbParameters Parameters { get; }

        public SqlBuilder Append(string sql, params SqlParameter[] args)
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

        public SqlBuilder Append(bool predicate, string sql, params SqlParameter[] args)
        {
            if (predicate) Append(sql, args);

            return this;
        }

        public SqlBuilder Append(bool predicate, string sql, string orSql, params SqlParameter[] args) =>
            predicate ? Append(sql, args) : Append(orSql, args);

        public SqlBuilder Append(SqlBuilder builder)
        {
            fragments.Append(builder.fragments);
            Parameters.Add(builder.Parameters);

            return this;
        }

        public override string ToString() => string.Join(" ", fragments);
    }
}