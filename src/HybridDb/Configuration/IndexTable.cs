using System.Data;

namespace HybridDb.Configuration
{
    public class IndexTable : Table
    {
        public IndexTable(string name) : base(name)
        {
            TableReferenceColumn = new SystemColumn("TableReference", typeof(string), new SqlColumn(DbType.String, 255));
            Register(TableReferenceColumn);
        }

        public SystemColumn TableReferenceColumn { get; private set; }
    }
}