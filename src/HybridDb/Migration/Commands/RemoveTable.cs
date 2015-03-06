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
            //store.RawExecute(
            //    string.Format("if not ({0}) begin create table {1} ({2}); end",
            //        GetTableExistsSql(store, Table.Name),
            //        store.FormatTableNameAndEscape(Table.Name),
            //        string.Join(", ", escaptedColumns)));
        }
    }
}