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

        public override void Execute(IDatabase database)
        {
            database.RawExecute($"drop table {database.FormatTableNameAndEscape(Tablename)};");
        }

        public override string ToString() => $"Remove table {Tablename}";
    }
}