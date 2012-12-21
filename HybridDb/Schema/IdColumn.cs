using System.Data;

namespace HybridDb.Schema
{
    public class IdColumn : IColumn
    {
        public string Name
        {
            get { return "Id"; }
        }

        public Column Column
        {
            get { return new Column(DbType.Guid, isPrimaryKey: true); }
        }

        public object Serialize(object value)
        {
            return value;
        }

        public object GetValue(object document)
        {
            return ((dynamic) document).Id;
        }
    }
}