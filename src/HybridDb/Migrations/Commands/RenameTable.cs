namespace HybridDb.Migrations.Commands
{
    public class RenameTable : SchemaMigrationCommand
    {
        public RenameTable(string oldTableName, string newTableName)
        {
            OldTableName = oldTableName;
            NewTableName = newTableName;
        }

        public string OldTableName { get; }
        public string NewTableName { get; }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";
    }

    public class RenameTableExecutor : DdlCommandExecutor<DocumentStore, RenameTable>
    {
        public override void Execute(DocumentStore store, RenameTable command)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename {store.Database.FormatTableNameAndEscape(command.OldTableName)}, {store.Database.FormatTableNameAndEscape(command.NewTableName)};")
                .ToString());
        }
    }

}