using System;

namespace HybridDb.Schema
{
    public class UserColumn : Column
    {
        public UserColumn(string columnName, SqlColumn sqlColumn)
        {
            Name = columnName;
            SqlColumn = sqlColumn;
        }

        public UserColumn(string name, Type type)
        {
            Name = name;
            SqlColumn = new SqlColumn(type);
        }

        public UserColumn(string name)
        {
            Name = name;
            SqlColumn = new SqlColumn();
        }
    }

    public class CollectionColumn : UserColumn
    {
        public CollectionColumn(string columnName, SqlColumn sqlColumn) : base(columnName, sqlColumn) { }
    }
}