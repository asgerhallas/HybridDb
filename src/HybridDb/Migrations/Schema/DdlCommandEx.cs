using System;
using HybridDb.Config;

namespace HybridDb.Migrations.Schema
{
    public static class DdlCommandEx
    {
        public static string BuildTableExistsSql(DocumentStore store, string tablename) =>
            string.Format(store.Database is SqlServerUsingRealTables
                    ? "object_id('{0}', 'U') is not null"
                    : "object_id('tempdb..{0}') is not null",
                store.Database.FormatTableName(tablename));

        public static SqlBuilder BuildColumnSql(Column column)
        {
            if (column.Type == null)
                throw new ArgumentException($"Column {column.Name} must have a type");

            var sql = new SqlBuilder();

            var sqlColumn = SqlTypeMap.Convert(column);
            sql.Append($"{column.DbType}");
            sql.Append(sqlColumn.Length != null, "(" + sqlColumn.Length + ")");
            sql.Append(column.Nullable, " NULL", " NOT NULL");
            sql.Append(column.DefaultValue != null, $" DEFAULT '{column.DefaultValue}'");
            sql.Append(column.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}