using System;
using System.Data;

namespace HybridDb.Schema
{
    public class DocumentColumn : Column
    {
        public DocumentColumn()
        {
            Name = "Document";
            SqlColumn = new SqlColumn(DbType.Binary, Int32.MaxValue);
        }
    }
}