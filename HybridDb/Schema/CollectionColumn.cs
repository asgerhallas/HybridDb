using System;

namespace HybridDb.Schema
{
    public class CollectionColumn : Column
    {
        public CollectionColumn(string columnName) : base(columnName, typeof(int))
        {
            Name = columnName;
            SqlColumn = new SqlColumn(typeof(int));
        }
    }
}