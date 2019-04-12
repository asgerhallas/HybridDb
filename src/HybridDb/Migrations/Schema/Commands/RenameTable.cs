namespace HybridDb.Migrations.Schema.Commands
{
    public class RenameTable : DdlCommand
    {
        public RenameTable(string oldTableName, string newTableName)
        {
            OldTableName = oldTableName;
            NewTableName = newTableName;
        }

        public string OldTableName { get; }
        public string NewTableName { get; }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";
    }
}