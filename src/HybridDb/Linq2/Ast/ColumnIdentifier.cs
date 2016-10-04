using System;

namespace HybridDb.Linq2.Ast
{
    public class ColumnIdentifier : SqlExpression
    {
        public ColumnIdentifier(Type type, string columnName)
        {
            Type = type;
            ColumnName = columnName;
        }

        public Type Type { get; }
        public string ColumnName { get; }
    }
}