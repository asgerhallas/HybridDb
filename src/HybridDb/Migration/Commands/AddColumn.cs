using HybridDb.Config;

namespace HybridDb.Migration.Commands
{
    public class AddColumn : SchemaMigrationCommand
    {
        public AddColumn(string tablename, Column column)
        {
            Tablename = tablename;
            Column = column;
        }

        public string Tablename { get; private set; }
        public Column Column { get; private set; }

        public override void Execute(DocumentStore store)
        {
            var sql = new SqlBuilder();
            sql.Append("alter table {0} add {1}", store.FormatTableNameAndEscape(Tablename), store.Escape(Column.Name));
            sql.Append(GetColumnSqlType(Column));
            
            store.RawExecute(sql.ToDynamicSql(), sql.Parameters);
        }
    }
}