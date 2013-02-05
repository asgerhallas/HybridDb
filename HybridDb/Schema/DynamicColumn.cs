using System;
using System.Data;

namespace HybridDb.Schema
{
    public class DynamicColumn : Column
    {
        public DynamicColumn(string name, Type type)
        {
            Name = name;
            SqlColumn = new SqlColumn(type);
        }

        public DynamicColumn(string name)
        {
            Name = name;
            SqlColumn = new SqlColumn();
        }
    }
}