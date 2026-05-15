using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class RenameTable : DdlCommand
    {
        public RenameTable(Table oldTableName, Table newTableName)
        {
            Safe = true;

            OldTableName = oldTableName;
            NewTableName = newTableName;
        }

        public Table OldTableName { get; }
        public Table NewTableName { get; }

        public override string ToString() => $"Rename table {OldTableName} to {NewTableName}";

        public override void Execute(DocumentStore store) =>
            store.Database.RawExecute(Sql.Empty
                .Append(store.Database is SqlServerUsingRealTables, "", "tempdb..")
                .Append($"sp_rename {OldTableName}, {NewTableName};"));
    }
}