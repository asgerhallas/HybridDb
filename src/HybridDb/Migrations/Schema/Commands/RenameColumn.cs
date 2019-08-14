using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class RenameColumn : DdlCommand
    {
        public RenameColumn(Table table, string oldColumnName, string newColumnName)
        {
            Safe = oldColumnName != "Document";

            Table = table;
            OldColumnName = oldColumnName;
            NewColumnName = newColumnName;
        }

        public Table Table { get; }
        public string OldColumnName { get; }
        public string NewColumnName { get; }

        public override string ToString() => $"Rename column {OldColumnName} on table {Table.Name} to {NewColumnName}";

        public override void Execute(DocumentStore store)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename '{store.Database.FormatTableNameAndEscape(Table.Name)}.{OldColumnName}', '{NewColumnName}', 'COLUMN'")
                .ToString());
        }
    }
}