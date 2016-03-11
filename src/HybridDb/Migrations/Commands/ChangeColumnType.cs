using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class ChangeColumnType : SchemaMigrationCommand
    {
        public ChangeColumnType(string tablename, Column column)
        {
            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; }
        public Column Column { get; }

        public override void Execute(IDatabase database)
        {
            var sql = new SqlBuilder();
            sql.Append("alter table {0} alter column {1}", database.FormatTableNameAndEscape(Tablename), database.Escape(Column.Name));
            sql.Append(GetColumnSqlType(Column));

            database.RawExecute(sql.ToString());
        }

        public override string ToString()
        {
            return $"Change type of column {Column} on table {Tablename} to {Column.Type}.";
        }
    }
}