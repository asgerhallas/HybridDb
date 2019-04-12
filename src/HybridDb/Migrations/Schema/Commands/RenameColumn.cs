using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class RenameColumn : DdlCommand
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

        public override string ToString() => $"Rename column {OldColumnName} on table {Table.Name} to {NewColumnName}";
    }
}