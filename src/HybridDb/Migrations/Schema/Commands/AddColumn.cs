using HybridDb.Config;

namespace HybridDb.Migrations.Schema.Commands
{
    public class AddColumn : SchemaMigrationCommand
    {
        public AddColumn(string tablename, Column column)
        {
            RequiresReprojectionOf = tablename;

            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; }
        public Column Column { get; }

        public override string ToString() => $"Add column {Column} to table {Tablename}.";
    }
}