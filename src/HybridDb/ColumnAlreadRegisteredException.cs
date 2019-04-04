using HybridDb.Config;

namespace HybridDb
{
    public class ColumnAlreadRegisteredException : HybridDbException
    {
        public ColumnAlreadRegisteredException(Table table, Column column)
            : base(string.Format("The table {0} already has a column named {1} and is not of the same type.", table.Name, column.Name)) { }
    }
}