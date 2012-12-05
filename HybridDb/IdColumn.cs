using System.Data;

namespace HybridDb
{
    public class IdColumn : IColumnConfiguration
    {
        public string Name
        {
            get { return "Id"; }
        }

        public Column Column
        {
            get { return new Column(DbType.Guid); }
        }

        public object GetValue(object document)
        {
            return ((dynamic) document).Id;
        }
    }
}