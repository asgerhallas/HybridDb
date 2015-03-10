using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migration.Commands
{
    public class CreateTable : SchemaMigrationCommand
    {
        public CreateTable(Table table)
        {
            Table = table;
        }

        public Table Table { get; private set; }

        public override void Execute(DocumentStore store)
        {
            if (!Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            //var escapedColumns =
            //    from column in columns
            //    let split = column.Split(' ')
            //    let name = split.First()
            //    let type = string.Join(" ", split.Skip(1))
            //    select store.Escape(name) + " " + type;

            var sql = new SqlBuilder();
            sql.Append("if not ({0}) begin create table {1} (",
                GetTableExistsSql(store, Table.Name),
                store.FormatTableNameAndEscape(Table.Name));

            var i = 0;
            foreach (var column in Table.Columns)
            {
                var sqlBuilder = new SqlBuilder()
                    .Append(store.Escape(column.Name))
                    .Append(GetColumnSqlType(column, i.ToString()));

                sql.Append(sqlBuilder);
                i++;
            }

            sql.Append("); end;");
            
            store.RawExecute(sql.ToDynamicSql(), sql.Parameters);

            //store.RawExecute(string.Format("if not ({0}) begin create table {1} ({2}); end",
            //    GetTableExistsSql(store, Table.Name),
            //    store.FormatTableNameAndEscape(Table.Name),
            //    string.Join(", ", escapedColumns)));
        }
    }
}