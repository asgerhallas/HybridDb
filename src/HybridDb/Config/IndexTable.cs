using System.Data;

namespace HybridDb.Config
{
    public class IndexTable : Table
    {
        public IndexTable(string name) : base(name)
        {
            //TODO: previously new SqlColumn(DbType.String, 255)...how to use fixed length?
            TableReferenceColumn = new SystemColumn("TableReference", typeof(string));
            Register(TableReferenceColumn);
        }

        public SystemColumn TableReferenceColumn { get; private set; }
    }
}