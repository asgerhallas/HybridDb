using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class CreateTable : SchemaMigrationCommand
    {
        public CreateTable(Table table)
        {
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
            sql.Append("if not ({0}) begin create table {1} (",
                GetTableExistsSql(database, Table.Name),
                database.FormatTableNameAndEscape(Table.Name));


            var i = 0;
            foreach (var column in Table.Columns)
            {
                var sqlBuilder = new SqlBuilder()
                    .Append(i > 0, ",")
                    .Append(database.Escape(column.Name))
                    .Append(GetColumnSqlType(column, i.ToString()));

                sql.Append(sqlBuilder);
                i++;
            }

            sql.Append("); end;");

            database.RawExecute(sql.ToString());
        }

        public override string ToString()
        {
            return $"Create table {Table} with columns {string.Join(", ", Table.Columns.Select(x => x.ToString()))}";
        }
    }
}