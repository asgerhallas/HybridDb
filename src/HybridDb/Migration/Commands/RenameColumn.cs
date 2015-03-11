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

        public override void Execute(Database database)
        {
            database.RawExecute(string.Format("{0}sp_rename '{1}.{2}', '{3}', 'COLUMN'",
                database.TableMode == TableMode.UseTempTables || database.TableMode == TableMode.UseGlobalTempTables ? "tempdb.." : "",
                database.FormatTableNameAndEscape(Table.Name),
                OldColumnName,
                NewColumnName));         
        }
    }
}