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

        public override void Execute(IDatabase database)
        {
            database.RawExecute($"sp_rename {database.FormatTableNameAndEscape(OldTableName)}, {database.FormatTableNameAndEscape(NewTableName)};");
        }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";
    }
}