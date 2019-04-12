namespace HybridDb.Migrations.Schema.Commands
{
    public class RemoveTable : DdlCommand
    {
        public RemoveTable(string tablename)
        {
            Unsafe = true;
            Tablename = tablename;
        }

        public string Tablename { get; }

        public override string ToString() => $"Remove table {Tablename}";
    }
}