using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using HybridDb.Config;

namespace HybridDb
{

    public class MyFormatter : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }

        public string Format(string fmt, object arg, IFormatProvider formatProvider)
        {
            if (arg == null) return string.Empty;

            if (fmt == "lcase")
                return arg.ToString().ToLower();
            else if (fmt == "ucase")
                return arg.ToString().ToUpper();
            else if (fmt == "nospace")
                return arg.ToString().Replace(" ", "");
            // Use this if you want to handle default formatting also
            else if (arg is IFormattable)
                return ((IFormattable)arg).ToString(fmt, CultureInfo.CurrentCulture);
            return arg.ToString();
        }
    }


    public class SqlBuilder<T>
    {
        public SqlBuilder(Action<Helper<T>> appender)
        {
            
        }
    }

    public class Helper<T> where T: class
    {
        public void Append(FormattableString sql);
        public T Col => (T)null; // duer ikke
        public 
    }

    public class SqlBuilder
    {
        readonly StringBuilder fragments;
        readonly List<SqlParameter> parameters;

        public SqlBuilder()
        {
            fragments = new StringBuilder(); 
            parameters = new List<SqlParameter>();
        }

        public IEnumerable<SqlParameter> Parameters => parameters;

        public SqlBuilder Append(FormattableString sql)
        {
            var sqlParameters = sql.GetArguments().Select((x, i) => new SqlParameter($"p{i}", x));

            return Append(sql.ToString(), sqlParameters);
        }

        public SqlBuilder Append(string sql, SqlParameter arg0, params SqlParameter[] args) => Append(sql, new[] {arg0}.Concat(args));

        public SqlBuilder Append(string sql, IEnumerable<SqlParameter> args)
        {
            foreach (var arg in args)
            {
                parameters.Add(arg);
            }

            if (fragments.Length != 0) fragments.Append(" ");

            fragments.Append(sql);

            return this;
        }

        public SqlBuilder AppendColumnName(Column column) => null;
            
            //Append(store.Database.Escape(column.Name), new SqlParameter[0]);

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
            parameters.AddRange(builder.parameters);
            return this;
        }

        public (string Sql, IReadOnlyList<SqlParameter> Parameters) Build(IDocumentStore store) => (null, null);

        public override string ToString() => string.Join(" ", fragments);
    }
}