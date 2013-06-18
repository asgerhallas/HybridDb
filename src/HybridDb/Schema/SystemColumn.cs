namespace HybridDb.Schema
{
    public class SystemColumn : Column
    {
        public SystemColumn(string columnName, SqlColumn sqlColumn) : base(columnName, sqlColumn)
        {
            Name = columnName;
            SqlColumn = sqlColumn;
        }
    }
}