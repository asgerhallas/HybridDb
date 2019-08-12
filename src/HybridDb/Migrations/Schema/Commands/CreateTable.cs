using System;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class CreateTable : DdlCommand
    {
        public CreateTable(Table table) => Table = table;

        public Table Table { get; }

        public override void Execute(DocumentStore store)
        {
            if (!Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            var sql = new SqlBuilder()
                .Append($"if not ({DdlCommandEx.BuildTableExistsSql(store, Table.Name)})")
                .Append($"begin create table {store.Database.FormatTableNameAndEscape(Table.Name)} (");

            foreach (var (column, i) in Table.Columns.Select((column, i) => (column, i)))
            {
                sql.Append(new SqlBuilder()
                    .Append(i > 0, ",")
                    .Append(store.Database.Escape(column.Name))
                    .Append(DdlCommandEx.BuildColumnSql(column)));
            }

            sql.Append(") end;");

            store.Database.RawExecute(sql.ToString(), schema: true);
        }

        public override string ToString() => $"Create table {Table} with columns {string.Join(", ", Table.Columns.Select(x => x.ToString()))}";
    }
}