using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class RemoveTable : DdlCommand
    {
        public RemoveTable(string tablename)
        {
            Safe = false;

            Tablename = tablename;
        }

        public string Tablename { get; }

        public override string ToString() => $"Remove table {Tablename}";

        public override void Execute(DocumentStore store)
        {
            store.Database.RawExecute(Sql.From($"drop table {new Table(Tablename)};"));
        }
    }
}