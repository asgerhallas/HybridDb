using System;

namespace HybridDb.Linq.Ast
{
    public class SqlConstantExpression : SqlExpression
    {
        public SqlConstantExpression(Type type, object value)
        {
            Type = type;
            Value = value;
        }

        public Type Type { get; private set; }
        public object Value { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Constant; }
        }
    }
}