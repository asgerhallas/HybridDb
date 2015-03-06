using System.Linq;
using HybridDb.Configuration;

namespace HybridDb.Migration.Commands
{
    public class AddColumn : SchemaMigrationCommand
    {
        public AddColumn(string tableName, Column column)
        {
            TableName = tableName;
            Column = column;
        }

        public string TableName { get; private set; }
        public Column Column { get; private set; }

        public override void Execute(DocumentStore store)
        {
            var sql = new SqlBuilder();
            sql.Append("alter table {0} add {1}", store.FormatTableNameAndEscape(TableName), store.Escape(Column.Name));
            sql.Append(GetColumnSqlType(Column));
            
            store.RawExecute(sql.ToDynamicSql(), sql.Parameters);
        }
    }
}