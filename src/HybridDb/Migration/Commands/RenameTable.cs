namespace HybridDb.Migration.Commands
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

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(string.Format("sp_rename {0}, {1};",
                store.FormatTableNameAndEscape(OldTableName),
                store.FormatTableNameAndEscape(NewTableName)));
        }
    }
}