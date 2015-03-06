using System;

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
            if (store.TableMode != TableMode.UseRealTables)
                throw new NotSupportedException("It is not possible to rename temp tables, so RenameTable is not supported when store is in test mode.");

            store.RawExecute(string.Format("sp_rename {0}, {1};",
                store.FormatTableNameAndEscape(OldTableName),
                store.FormatTableNameAndEscape(NewTableName)));
        }
    }
}