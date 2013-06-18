namespace HybridDb.Linq.Ast
{
    public class SqlConstantExpression : SqlExpression
    {
        public SqlConstantExpression(object value)
        {
            Value = value;
        }

        public object Value { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Constant; }
        }
    }
}