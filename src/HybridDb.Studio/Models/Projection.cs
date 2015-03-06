namespace HybridDb.Studio.Models
{
    public class Projection
    {
        public string Column { get; private set; }
        public object Value { get; set; }

        public Projection(string column, object value)
        {
            Column = column;
            Value = value;
        }
    }
}