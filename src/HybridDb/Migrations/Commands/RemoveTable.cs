namespace HybridDb.Migrations.Commands
{
    public class RemoveTable : SchemaMigrationCommand
    {
        public RemoveTable(string tablename)
        {
            Unsafe = true;
            Tablename = tablename;
        }

        public string Tablename { get; }

        public override string ToString() => $"Remove table {Tablename}";
    }

    public class RemoveTableExecutor : DdlCommandExecutor<DocumentStore, RemoveTable>
    {
        public override void Execute(DocumentStore store, RemoveTable command)
        {
            store.Database.RawExecute($"drop table {store.Database.FormatTableNameAndEscape(command.Tablename)};");
        }
    }
}