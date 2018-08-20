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

        public Table Table { get; }
        public string OldColumnName { get; }
        public string NewColumnName { get; }

        public override void Execute(IDatabase database)
        {
            database.RawExecute($"sp_rename '{database.FormatTableNameAndEscape(Table.Name)}.{OldColumnName}', '{NewColumnName}', 'COLUMN'");         
        }

        public override string ToString() => $"Rename column {OldColumnName} on table {Table.Name} to {NewColumnName}";
    }
}