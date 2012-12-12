using System.Data;

namespace HybridDb
{
    public class EtagColumn : IColumn
    {
        public string Name { get { return "Etag"; } }
        public Column Column { get { return new Column(DbType.Guid); } }
    }
}