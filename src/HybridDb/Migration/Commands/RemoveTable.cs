namespace HybridDb.Migration.Commands
{
    public class RemoveTable : SchemaMigrationCommand
    {
        public RemoveTable(string tablename)
        {
            Unsafe = true;
            Tablename = tablename;
        }

        public string Tablename { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(string.Format("drop table {0};", store.FormatTableNameAndEscape(Tablename)));
        }
    }
}