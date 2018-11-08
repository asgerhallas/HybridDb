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

        public override void Execute(IDatabase database)
        {
            if (database is SqlServerUsingRealTables)
            {
                var sql = new SqlBuilder();
                sql.Append($"alter table {database.FormatTableNameAndEscape(Tablename)} add {database.Escape(Column.Name)}");
                sql.Append(GetColumnSqlType(Column));

                database.RawExecute(sql.ToString());
            }
        }

        public override string ToString()
        {
            return string.Format("Add column {0} to table {1}.", Column, Tablename);
        }
    }
}