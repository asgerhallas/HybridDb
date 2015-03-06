using System;

namespace HybridDb.Config
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