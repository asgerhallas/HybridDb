using HybridDb.Schema;

namespace HybridDb.Studio.Models
{
    public class Projection
    {
        public IColumn Column { get; private set; }
        public object Value { get; set; }

        public Projection(IColumn column, object value)
        {
            Column = column;
            Value = value;
        }
    }
}