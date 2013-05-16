namespace HybridDb.Schema
{
    public class SystemColumn : Column
    {
        public SystemColumn(string columnName, SqlColumn sqlColumn)
        {
            Name = columnName;
            SqlColumn = sqlColumn;
        }
    }
}