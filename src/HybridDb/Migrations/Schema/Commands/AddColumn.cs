using HybridDb.Config;

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
            store.Database.RawExecute(
                new SqlBuilder()
                    .Append($"alter table {store.Database.FormatTableNameAndEscape(Tablename)} add {store.Database.Escape(Column.Name)}")
                    .Append(DdlCommandEx.BuildColumnSql(Column))
                    .ToString(),
                schema: true,
                commandTimeout: 300);
    }
}