using HybridDb.Config;

namespace HybridDb.Migration.Commands
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

        public Table Table { get; private set; }
        public string OldColumnName { get; private set; }
        public string NewColumnName { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(
                string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", store.FormatTableNameAndEscape(Table.Name), OldColumnName, NewColumnName));
        }
    }
}