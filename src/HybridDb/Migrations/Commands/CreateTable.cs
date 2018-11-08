using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class CreateTable : SchemaMigrationCommand
    {
        readonly bool inMem;

        public CreateTable(Table table, bool inMem = false)
        {
            this.inMem = inMem;
            Table = table;
        }

        public Table Table { get; }

        public override void Execute(IDatabase database)
        {
            if (!Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            var sql = new SqlBuilder();
            sql.Append($"if not ({GetTableExistsSql(database, Table.Name)})")
               .Append($"begin create table {database.FormatTableNameAndEscape(Table.Name)} (");

            var i = 0;
            foreach (var column in Table.Columns)
            {
                var sqlBuilder = new SqlBuilder()
                    .Append(i > 0, ",")
                    .Append(database.Escape(column.Name))
                    .Append(GetColumnSqlType(column, i.ToString(), inMem));

                sql.Append(sqlBuilder);
                i++;
            }

            sql.Append($") {(inMem ? " WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY)" : "")}; end;");

            database.RawExecute(sql.ToString(), schema: true);
        }

        public override string ToString()
        {
            return $"Create table {Table} with columns {string.Join(", ", Table.Columns.Select(x => x.ToString()))}";
        }
    }
}