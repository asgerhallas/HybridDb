using HybridDb.Config;

namespace HybridDb.Migrations.Commands
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

        public override void Execute(IDatabase database)
        {
            database.RawExecute(string.Format("{0}sp_rename '{1}.{2}', '{3}', 'COLUMN'",
                database is SqlServerUsingRealTables ? "" : "tempdb..",
                database.FormatTableNameAndEscape(Table.Name),
                OldColumnName,
                NewColumnName));         
        }

        public override string ToString()
        {
            return string.Format("Rename column {0} on table {1} to {2}", OldColumnName, Table.Name, NewColumnName);
        }
    }
}