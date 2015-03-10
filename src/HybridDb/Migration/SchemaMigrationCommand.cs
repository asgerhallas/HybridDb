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
            RequiresReprojection = false;
        }

        public bool Unsafe { get; protected set; }
        public bool RequiresReprojection { get; protected set; }

        protected string GetTableExistsSql(DocumentStore store, string tablename)
        {
            return string.Format(store.TableMode == TableMode.UseRealTables
                ? "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = ''{0}'')"
                : "OBJECT_ID(''tempdb..{0}'') is not null",
                store.FormatTableName(tablename));
        }

        protected SqlBuilder GetColumnSqlType(Column column, string defaultValuePostfix)
        {
            if (column.SqlColumn.Type == null)
                throw new ArgumentException(string.Format("Column {0} must have a type", column.Name));

            var sql = new SqlBuilder();

            sql.Append(new SqlParameter { DbType = (DbType)column.SqlColumn.Type }.SqlDbType.ToString());
            sql.Append(column.SqlColumn.Length != null, "(" + (column.SqlColumn.Length == Int32.MaxValue ? "MAX" : column.SqlColumn.Length.ToString()) + ")");
            sql.Append(column.SqlColumn.Nullable, "NULL").Or("NOT NULL");
            sql.Append(column.SqlColumn.DefaultValue != null,
                       string.Format("DEFAULT @DefaultValue{0}", defaultValuePostfix),
                       new Parameter
                       {
                           Name = string.Format("DefaultValue{0}", defaultValuePostfix),
                           DbType = column.SqlColumn.Type,
                           Value = column.SqlColumn.DefaultValue
                       });
            sql.Append(column.SqlColumn.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }

        protected SqlBuilder GetColumnSqlType(Column column)
        {
            if (column.SqlColumn.Type == null)
                throw new ArgumentException(string.Format("Column {0} must have a type", column.Name));

            var sql = new SqlBuilder();

            sql.Append(new SqlParameter { DbType = (DbType)column.SqlColumn.Type }.SqlDbType.ToString());
            sql.Append(column.SqlColumn.Length != null, "(" + (column.SqlColumn.Length == Int32.MaxValue ? "MAX" : column.SqlColumn.Length.ToString()) + ")");
            sql.Append(column.SqlColumn.Nullable, "NULL").Or("NOT NULL");
            sql.Append(column.SqlColumn.DefaultValue != null,
                       "DEFAULT @DefaultValue",
                       new Parameter
                       {
                           Name = "DefaultValue",
                           DbType = column.SqlColumn.Type,
                           Value = column.SqlColumn.DefaultValue
                       });
            sql.Append(column.SqlColumn.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}