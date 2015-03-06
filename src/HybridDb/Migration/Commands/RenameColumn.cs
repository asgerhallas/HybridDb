namespace HybridDb.Migration.Commands
{
    public class RenameColumn : SchemaMigrationCommand
    {
        public RenameColumn(string tablename, string oldColumnName, string newColumnName)
        {
            Unsafe = oldColumnName == "Document";

            Tablename = tablename;
            OldColumnName = oldColumnName;
            NewColumnName = newColumnName;
        }

        public string Tablename { get; private set; }
        public string OldColumnName { get; private set; }
        public string NewColumnName { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(
                string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", store.FormatTableNameAndEscape(Tablename), OldColumnName, NewColumnName));
        }
    }
}