namespace HybridDb.Migrations.Commands
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

        public override void Execute(IDatabase database)
        {
            if (database is SqlServerUsingTempTables)
            {
                database.RawExecute(string.Format("select * into {1} from {0}; drop table {0};",
                    database.FormatTableNameAndEscape(OldTableName),
                    database.FormatTableNameAndEscape(NewTableName)));
            }
            else
            {
                database.RawExecute(string.Format("sp_rename {0}, {1};",
                    database.FormatTableNameAndEscape(OldTableName),
                    database.FormatTableNameAndEscape(NewTableName)));
            }
        }

        public override string ToString()
        {
            return string.Format("Rename table {0} to {1}", OldTableName, NewTableName);
        }
    }
}