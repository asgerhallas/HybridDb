using System;
using System.Linq;
using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class CreateTable : DdlCommand
    {
        public CreateTable(Table table)
        {
            Safe = true;
            Table = table;
        }

        public Table Table { get; }

        public override void Execute(DocumentStore store)
        {
            if (!Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            var sql = Sql.Empty
                .Append($"if not ({DdlCommandEx.BuildTableExistsSql(store, Table.Name):@})")
                .Append($"begin create table {Table} (");

            foreach (var (column, i) in Table.Columns.Select((column, i) => (column, i)))
            {
                sql.Append(Sql.Empty
                    .Append(i > 0, ",")
                    .Append($"{column}")
                    .Append(DdlCommandEx.BuildColumnSql(column)));
            }

            sql.Append(") end;");

            store.Database.RawExecute(sql, schema: true);
        }

        public override string ToString() => $"Create table {Table} with columns {string.Join(", ", Table.Columns.Select(x => x.ToString()))}";
    }
}