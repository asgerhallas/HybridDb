namespace HybridDb.Migrations.Schema.Commands
{
    public class RenameTable : DdlCommand
    {
        public RenameTable(string oldTableName, string newTableName)
        {
            Safe = true;

            OldTableName = oldTableName;
            NewTableName = newTableName;
        }

        public string OldTableName { get; }
        public string NewTableName { get; }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";

        public override void Execute(DocumentStore store)
        {
            store.Database.RawExecute(new SqlBuilder()
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename {store.Database.FormatTableNameAndEscape(OldTableName)}, {store.Database.FormatTableNameAndEscape(NewTableName)};")
                .ToString());
        }
    }
}