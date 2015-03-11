using System;
using System.Data;
using System.Data.SqlClient;
using HybridDb.Config;

namespace HybridDb.Migration
{
    public abstract class SchemaMigrationCommand : MigrationCommand
    {
        protected SchemaMigrationCommand()
        {
            Unsafe = false;
            RequiresReprojectionOf = null;
        }

        public bool Unsafe { get; protected set; }
        public string RequiresReprojectionOf { get; protected set; }

        protected string GetTableExistsSql(Database db, string tablename)
        {
            return string.Format(db.TableMode == TableMode.UseRealTables
                ? "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')"
                : "OBJECT_ID('tempdb..{0}') is not null",
                db.FormatTableName(tablename));
        }

        protected SqlBuilder GetColumnSqlType(Column column, string defaultValuePostfix = "")
        {
            if (column.Type == null)
                throw new ArgumentException(string.Format("Column {0} must have a type", column.Name));

            var sql = new SqlBuilder();

            var sqlColumn = SqlTypeMap.GetDbType(column);
            sql.Append(new SqlParameter { DbType = sqlColumn.DbType }.SqlDbType.ToString());
            sql.Append(sqlColumn.Length != null, "(" + (sqlColumn.Length == Int32.MaxValue ? "MAX" : sqlColumn.Length.ToString()) + ")");
            sql.Append(column.Nullable, "NULL").Or("NOT NULL");
            sql.Append(column.DefaultValue != null, "DEFAULT '{0}'", column.DefaultValue);
            sql.Append(column.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}