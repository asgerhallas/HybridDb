using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class RenameColumn : SchemaMigrationCommand
    {
        public RenameColumn(Table table, string oldColumnName, string newColumnName)
        {
            Unsafe = oldColumnName == "Document";

            Table = table;
            OldColumnName = oldColumnName;
            NewColumnName = newColumnName;
        }

        public Table Table { get; }
        public string OldColumnName { get; }
        public string NewColumnName { get; }

        public override string ToString() => $"Rename column {OldColumnName} on table {Table.Name} to {NewColumnName}";
    }

    public class RenameColumnExecutor : DdlCommandExecutor<DocumentStore, RenameColumn>
    {
        public override void Execute(DocumentStore store, RenameColumn command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename '{store.Database.FormatTableNameAndEscape(command.Table.Name)}.{command.OldColumnName}', '{command.NewColumnName}', 'COLUMN'")
                .ToString());
        }
    }
}