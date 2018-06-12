using System.Data;

namespace HybridDb.Config
{
    public class SqlColumn
    {
        public SqlColumn(SqlDbType dbType, string length)
        {
            DbType = dbType;
            Length = length;
        }

        public SqlDbType DbType { get; private set; }
        public string Length { get; private set; }
    }
}