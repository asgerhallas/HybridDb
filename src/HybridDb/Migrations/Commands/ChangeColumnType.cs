using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class ChangeColumnType : SchemaMigrationCommand
    {
        static readonly IEnumerable<string> constraintQueries = new List<string>
        {
            @"
                SELECT default_constraints.name
                FROM sys.all_columns
                INNER JOIN sys.tables ON all_columns.object_id = tables.object_id
                INNER JOIN sys.schemas ON tables.schema_id = schemas.schema_id
                INNER JOIN sys.default_constraints ON all_columns.default_object_id = default_constraints.object_id
                WHERE tables.name = @TableName AND all_columns.name = @ColumnName",
            @"
                SELECT ct.constraint_name
                FROM information_schema.table_constraints AS ct
                JOIN information_schema.key_column_usage AS k
                ON ct.table_name = k.table_name
                AND ct.constraint_catalog = k.constraint_catalog
                AND ct.constraint_schema = k.constraint_schema 
                AND ct.constraint_name = k.constraint_name
                WHERE ct.constraint_type = 'primary key'
                AND k.table_name = @TableName
                AND k.column_name = @ColumnName"
        }; 

        static readonly IEnumerable<string> tempTablesConstraintQueries = new List<string>
        {
            @"
                SELECT c.name
                FROM tempdb.dbo.sysobjects AS t
                INNER JOIN tempdb.dbo.sysobjects AS c ON c.parent_obj = t.id AND c.type = 'D'
                WHERE t.id = OBJECT_ID('tempdb.dbo.' + @TableName) AND c.name LIKE ('%__' + SUBSTRING(@ColumnName, 1, 5) + '__%')",
            @"
                SELECT k.constraint_name
                FROM tempdb.dbo.sysobjects AS t
                INNER JOIN tempdb.information_schema.table_constraints AS ct ON ct.table_name = t.name
                INNER JOIN tempdb.information_schema.key_column_usage AS k
                ON ct.table_name = k.table_name
                AND ct.constraint_catalog = k.constraint_catalog
                AND ct.constraint_schema = k.constraint_schema 
                AND ct.constraint_name = k.constraint_name
                WHERE ct.constraint_type = 'primary key'
                AND t.id = OBJECT_ID('tempdb.dbo.' + @TableName)
                AND k.column_name = @ColumnName"
        }; 

        public ChangeColumnType(string tableName, Column column)
        {
            Unsafe = true;
            TableName = tableName;
            Column = column;
        }

        public string TableName { get; }
        public Column Column { get; }

        public override void Execute(IDatabase database)
        {
            var builder = new SqlBuilder();
            var formattedAndEscapedTableName = database.FormatTableNameAndEscape(TableName);
            var escapedColumnName = database.Escape(Column.Name);

            //DROP CONSTRAINTS
            foreach (var constraintName in (database is SqlServerUsingTempTables ? tempTablesConstraintQueries : constraintQueries)
                .Select(x => database.RawQuery<string>(x, new { TableName = database.FormatTableName(TableName), ColumnName = Column.Name }))
                .Select(x => x.SingleOrDefault())
                .Where(x => x != null))
            {
                builder.Append($"ALTER TABLE {formattedAndEscapedTableName} DROP CONSTRAINT {database.Escape(constraintName)};");
            }

            
            builder
                //ALTER TABLE
                .Append(GetAlterTableStatement(formattedAndEscapedTableName, escapedColumnName))

                //DEFAULT CONSTRAINT
                .Append(Column.DefaultValue != null, $"ALTER TABLE {formattedAndEscapedTableName} ADD DEFAULT '{Column.DefaultValue}' FOR {escapedColumnName};")

                //PRIMAY KEY
                .Append(Column.IsPrimaryKey, $"ALTER TABLE {formattedAndEscapedTableName} ADD PRIMARY KEY ({escapedColumnName});");

            database.RawExecute(builder.ToString());
        }

        string GetAlterTableStatement(string formattedAndEscapedTableName, string escapedColumnName)
        {
            var builder = new SqlBuilder();
            var sqlColumn = SqlTypeMap.Convert(Column);

            builder
                .Append($"ALTER TABLE {formattedAndEscapedTableName} ALTER COLUMN {escapedColumnName}")
                .Append(new SqlParameter {DbType = sqlColumn.DbType}.SqlDbType.ToString())
                .Append(sqlColumn.Length != null, "(" + sqlColumn.Length + ")")
                .Append(Column.Nullable, "NULL").Or("NOT NULL")
                .Append(";");

            return builder.ToString();
        }

        public override string ToString()
        {
            return $"Change type of column {Column} on table {TableName} to {Column.Type}.";
        }
    }
}