using System;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Migrations.Schema
{
    public static class DdlCommandExecutors
    {
        public static void Execute(DocumentStore store, CreateTable command)
        {
            if (!command.Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            var sql = new SqlBuilder()
                .Append($"if not ({BuildTableExistsSql(store, command.Table.Name)})")
                .Append($"begin create table {store.Database.FormatTableNameAndEscape(command.Table.Name)} (");

            foreach (var (column, i) in command.Table.Columns.Select((column, i) => (column, i)))
            {
                sql.Append(new SqlBuilder()
                    .Append(i > 0, ",")
                    .Append(store.Database.Escape(column.Name))
                    .Append(BuildColumnSql(column)));
            }

            sql.Append(") end;");

            store.Database.RawExecute(sql.ToString(), schema: true);
        }

        public static void Execute(DocumentStore store, RemoveTable command)
        {
            store.Database.RawExecute($"drop table {store.Database.FormatTableNameAndEscape(command.Tablename)};");
        }

        public static void Execute(DocumentStore store, RenameTable command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename {store.Database.FormatTableNameAndEscape(command.OldTableName)}, {store.Database.FormatTableNameAndEscape(command.NewTableName)};")
                .ToString());
        }

        public static void Execute(DocumentStore store, AddColumn command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append($"alter table {store.Database.FormatTableNameAndEscape(command.Tablename)} add {store.Database.Escape(command.Column.Name)}")
                .Append(BuildColumnSql(command.Column))
                .ToString());
        }

        public static void Execute(DocumentStore store, RemoveColumn command)
        {
            // TODO: sletter kun den første ser det ud til?
            var dropConstraints = new SqlBuilder()
                .Append("DECLARE @ConstraintName nvarchar(200)")
                .Append("SELECT @ConstraintName = Name FROM SYS.DEFAULT_CONSTRAINTS ")
                .Append($"WHERE PARENT_OBJECT_ID = OBJECT_ID('{command.Table.Name}') ")
                .Append($"AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = N'{command.Name}' AND object_id = OBJECT_ID(N'{command.Table.Name}'))")
                .Append($"IF @ConstraintName IS NOT NULL ")
                .Append($"EXEC('ALTER TABLE {command.Table.Name} DROP CONSTRAINT ' + @ConstraintName)");

            store.Database.RawExecute(dropConstraints.ToString());

            store.Database.RawExecute($"alter table {store.Database.FormatTableNameAndEscape(command.Table.Name)} drop column {store.Database.Escape(command.Name)};");
        }

        public static void Execute(DocumentStore store, RenameColumn command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename '{store.Database.FormatTableNameAndEscape(command.Table.Name)}.{command.OldColumnName}', '{command.NewColumnName}', 'COLUMN'")
                .ToString());
        }

        public static void Execute(DocumentStore store, SqlCommand command)
        {
            var sql = new SqlBuilder();
            command.Builder(sql, store.Database);
            store.Database.RawExecute(sql.ToString());
        }

        static string BuildTableExistsSql(DocumentStore store, string tablename) =>
            string.Format(store.Database is SqlServerUsingRealTables
                    ? "object_id('{0}', 'U') is not null"
                    : "OBJECT_ID('tempdb..{0}') is not null",
                store.Database.FormatTableName(tablename));

        static SqlBuilder BuildColumnSql(Column column)
        {
            if (column.Type == null)
                throw new ArgumentException($"Column {column.Name} must have a type");

            var sql = new SqlBuilder();

            var sqlColumn = SqlTypeMap.Convert(column);
            sql.Append(column.DbType.ToString());
            sql.Append(sqlColumn.Length != null, "(" + sqlColumn.Length + ")");
            sql.Append(column.Nullable, " NULL", " NOT NULL");
            sql.Append(column.DefaultValue != null, $" DEFAULT '{column.DefaultValue}'");
            sql.Append(column.IsPrimaryKey, " PRIMARY KEY");

            return sql;
        }
    }
}