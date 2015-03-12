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

        public string Tablename { get; private set; }
        public Column Column { get; private set; }

        public override void Execute(Database database)
        {
            var sql = new SqlBuilder();
            sql.Append("alter table {0} add {1}", database.FormatTableNameAndEscape(Tablename), database.Escape(Column.Name));
            sql.Append(GetColumnSqlType(Column));

            database.RawExecute(sql.ToString());
        }
    }
}