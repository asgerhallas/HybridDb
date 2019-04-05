using System;
using System.Linq;

namespace HybridDb.Migrations.Commands {
    public static class DdlCommandExecutors
    {
        public static void Execute(DocumentStore store, CreateTable command)
        {
            if (!command.Table.Columns.Any())
            {
                throw new InvalidOperationException("Cannot create a table with no columns.");
            }

            var sql = new SqlBuilder()
                .Append($"if not ({store.BuildTableExistsSql(command.Table.Name)})")
                .Append($"begin create table {store.Database.FormatTableNameAndEscape(command.Table.Name)} (");

            foreach (var (column, i) in command.Table.Columns.Select((column, i) => (column, i)))
            {
                sql.Append(new SqlBuilder()
                    .Append(i > 0, ",")
                    .Append(store.Database.Escape(column.Name))
                    .Append(store.BuildColumnSql(column)));
            }

            sql.Append(") end;");

            store.Database.RawExecute(sql.ToString(), schema: true);
        }
 
    }
}