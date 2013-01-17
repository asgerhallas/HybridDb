﻿namespace HybridDb.Linq.Ast
{
    public class SqlConstantExpression : SqlExpression
    {
        public object Value { get; private set; }

        public SqlConstantExpression(object value)
        {
            Value = value;
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Constant; }
        }
    }
}