using System.Data;

namespace HybridDb.Config
{
    public class SqlColumn
    {
        public SqlColumn(DbType dbType, string length)
        {
            DbType = dbType;
            Length = length;
        }

        public DbType DbType { get; private set; }
        public string Length { get; private set; }
    }
}