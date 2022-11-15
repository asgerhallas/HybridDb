using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;

namespace HybridDb.Migrations.BuiltIn
{
    /// <summary>
    /// Migrate messages table from HybridDb versions <= 3.11.0.
    /// Must be used before auto migrations.
    /// </summary>
    public class HybridDb_3_11_0 : DdlCommand
    {
        public override void Execute(DocumentStore store)
        {
            foreach (var table in store.Configuration.Tables.Values.OfType<QueueTable>())
            {
                var tableNameEscaped = store.Database.FormatTableNameAndEscape(table.Name);
                var oldTableNameEscaped = store.Database.FormatTableNameAndEscape($"{table.Name}_old");

                // Rename the old table and the primary key
                store.Execute(new RenameTable(table.Name, $"{table.Name}_old"));
                store.Database.RawExecute($"sp_rename [PK_{table.Name}], [PK_{table.Name}_old]");

                // Create the new table as it should be, with correct ordering of columns
                store.Execute(table.GetCreateCommand());

                var columnNames = store.Database.RawQuery<string>(
                    "select column_name from information_schema.columns where table_name = @TableName",
                    new { TableName = $"{table.Name}_old" });

                var columns = string.Join(", ", columnNames);

                // Move the data from the old to the new table
                store.Database.RawExecute(@$"
                    insert into {tableNameEscaped} ({columns})
                    select {columns} from {oldTableNameEscaped};");

                store.Database.RawExecute($"drop table {oldTableNameEscaped};");
            }
        }

        public override string ToString() => "Add new primary index to messages table.";
    }
}