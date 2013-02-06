using System.Data;

namespace HybridDb.Schema
{
    public class IdColumn : Column
    {
        public IdColumn()
        {
            Name = "Id";
            SqlColumn = new SqlColumn(DbType.Guid, isPrimaryKey: true);
        }

        public object GetValue(object document)
        {
            return ((dynamic) document).Id;
        }
    }
}