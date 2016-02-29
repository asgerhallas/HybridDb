using System.Data;

namespace HybridDb
{
    public class Parameter
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public DbType? DbType { get; set; }
        public string Size { get; set; }
    }
}