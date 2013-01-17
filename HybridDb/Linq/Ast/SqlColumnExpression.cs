using System;

namespace HybridDb.Linq.Ast
{
    public class SqlColumnExpression : SqlExpression
    {
        readonly Type type;
        readonly string columnName;

        public SqlColumnExpression(Type type, string columnName)
        {
            this.type = type;
            this.columnName = columnName;
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Column; }
        }

        public string ColumnName
        {
            get { return columnName; }
        }

        public Type Type
        {
            get { return type; }
        }
    }
}