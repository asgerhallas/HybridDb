namespace HybridDb.Migrations.Commands
{
    public class RenameTable : SchemaMigrationCommand
    {
        public RenameTable(string oldTableName, string newTableName)
        {
            OldTableName = oldTableName;
            NewTableName = newTableName;
        }

        public string OldTableName { get; private set; }
        public string NewTableName { get; private set; }

        public override void Execute(IDatabase database)
        {
            if (database is SqlServerUsingRealTables)
            {
                database.RawExecute($"sp_rename {database.FormatTableNameAndEscape(OldTableName)}, {database.FormatTableNameAndEscape(NewTableName)};");
            }

            // Not supported for temp tables
        }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";
    }
}