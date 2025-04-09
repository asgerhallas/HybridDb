using HybridDb.Config;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class AddColumn : DdlCommand
    {
        public AddColumn(string tablename, Column column)
        {
            Safe = true;
            RequiresReprojectionOf = tablename;

            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; }
        public Column Column { get; }

        public override string ToString() => $"Add column {Column} to table {Tablename}.";

        public override void Execute(DocumentStore store) =>
            store.Database.RawExecute(Sql
                .From($"alter table {store.Database.FormatTableNameAndEscape(Tablename):@} add {Column}")
                .Append(DdlCommandEx.BuildColumnSql(Column)));
    }
}