using HybridDb.Config;

namespace HybridDb.Migration.Commands
{
    public class RemoveColumn : SchemaMigrationCommand
    {
        public RemoveColumn(Table table, string columnname)
        {
            Unsafe = columnname == "Document";

            Table = table;
            Columnname = columnname;
        }

        public Table Table { get; private set; }
        public string Columnname { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(string.Format("alter table {0} drop column {1};", store.FormatTableNameAndEscape(Table.Name), store.Escape(Columnname)));
        }
    }
}