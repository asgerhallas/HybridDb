using System.Data;

namespace HybridDb
{
    public class Parameter
    {
        public Parameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public object Value { get; set; }
        public SqlDbType? DbType { get; set; }
        public string Size { get; set; }
    }
}