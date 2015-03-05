using System;

namespace HybridDb.Schema
{
    public class SystemColumn : Column
    {
        public SystemColumn(string columnName, Type type, SqlColumn sqlColumn) : base(columnName, type, sqlColumn)
        {
            Name = columnName;
            SqlColumn = sqlColumn;
        }
    }
}