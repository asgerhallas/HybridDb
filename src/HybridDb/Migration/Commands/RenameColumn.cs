using System;

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
            if (store.TableMode != TableMode.UseRealTables)
                throw new NotSupportedException("It is not possible to rename columns on temp tables, therefore RenameColumn is not supported when store is in test mode.");

            store.RawExecute(
                string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", store.FormatTableNameAndEscape(Tablename), OldColumnName, NewColumnName));
        }
    }
}