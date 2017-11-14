using System;

namespace HybridDb.Linq.Ast
{
    public class SqlColumnExpression : SqlExpression
    {
        public SqlColumnExpression(Type type, string columnName)
        {
            Type = type;
            ColumnName = columnName;
        }

        public override SqlNodeType NodeType => SqlNodeType.Column;

        public string ColumnName { get; }
        public Type Type { get; }
    }
}