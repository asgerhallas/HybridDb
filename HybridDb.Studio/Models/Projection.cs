using HybridDb.Schema;

namespace HybridDb.Studio.Models
{
    public class Projection
    {
        public Column Column { get; private set; }
        public object Value { get; set; }

        public Projection(Column column, object value)
        {
            Column = column;
            Value = value;
        }
    }
}