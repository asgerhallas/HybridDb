using HybridDb.Config;

namespace HybridDb.Migration.Commands
{
    public class RemoveColumn : SchemaMigrationCommand
    {
        public RemoveColumn(string tablename, Column column)
        {
            Unsafe = column.Name == "Document";

            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; private set; }
        public Column Column { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(string.Format("alter table {0} drop column {1};", store.FormatTableNameAndEscape(Tablename), store.Escape(Column.Name)));
        }
    }
}