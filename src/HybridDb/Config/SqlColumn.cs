using System.Data;

namespace HybridDb.Config
{
    public class SqlColumn
    {
        public SqlColumn(DbType dbType, int? length)
        {
            DbType = dbType;
            Length = length;
        }

        public DbType DbType { get; private set; }
        public int? Length { get; private set; }
    }
}