using System;
using System.Data;
using System.Data.SqlClient;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public abstract class SchemaMigrationCommand
    {
        protected SchemaMigrationCommand()
        {
            Unsafe = false;
            RequiresReprojectionOf = null;
        }

        public bool Unsafe { get; protected set; }
        public string RequiresReprojectionOf { get; protected set; }

        public abstract void Execute(IDatabase database);
        public new abstract string ToString();

        protected string GetTableExistsSql(IDatabase db, string tablename)
        {
            return string.Format(db is SqlServerUsingRealTables || db is SqlServerUsingTempDb
                ? "object_id('{0}', 'U') is not null"
                : "OBJECT_ID('tempdb..{0}') is not null",
                db.FormatTableName(tablename));
        }

        protected SqlBuilder GetColumnSqlType(Column column, string defaultValuePostfix = "")
        {
            if (column.Type == null)
                throw new ArgumentException($"Column {column.Name} must have a type");

            var sql = new SqlBuilder();

            var sqlColumn = SqlTypeMap.Convert(column);
            sql.Append(column.DbType.ToString());
            sql.Append(sqlColumn.Length != null, "(" + sqlColumn.Length + ")");
            sql.Append(column.Nullable, "NULL").Or("NOT NULL");
            sql.Append(column.DefaultValue != null, $"DEFAULT '{column.DefaultValue}'");
            sql.Append(column.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}