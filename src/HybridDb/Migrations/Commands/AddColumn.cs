using HybridDb.Config;

namespace HybridDb.Migrations.Commands
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

        public override void Execute(IDatabase database)
        {
            var sql = new SqlBuilder();
            sql.Append($"alter table {database.FormatTableNameAndEscape(Tablename)} add {database.Escape(Column.Name)}");
            sql.Append(GetColumnSqlType(Column));

            database.RawExecute(sql.ToString());
        }

        public override string ToString() => $"Add column {Column} to table {Tablename}.";
    }
}