using System.Data;

namespace HybridDb.Schema
{
    public class EtagColumn : IColumn
    {
        public string Name { get { return "Etag"; } }
        public Column Column { get { return new Column(DbType.Guid); } }
        
        public object Serialize(object value)
        {
            return value;
        }
    }
}