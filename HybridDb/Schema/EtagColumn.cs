using System.Data;

namespace HybridDb.Schema
{
    public class EtagColumn : Column
    {
        public EtagColumn()
        {
            Name = "Etag";
            SqlColumn = new SqlColumn(DbType.Guid);
        }
    }
}